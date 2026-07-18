# Communication & Mail — Test and Acceptance Plan (CM-11 results)

Branch `Mail-Com`. This replaces the implementation pack's generic template with what was
actually run against this build, what passed, and what could not be exercised in this sandbox
(disclosed explicitly rather than silently assumed).

## Automated tests — what actually ran

Full backend suite: **367 passed, 0 failed, 11 skipped** (`dotnet test
backend/Garmetix.Api.Tests/Garmetix.Api.Tests.csproj`), zero regressions across all 10 CM
stages that touched code. The 11 skips are all Postgres-gated (`[PostgresFact]`, requires
`GARMETIX_TEST_POSTGRES`) — consistent with every other Postgres-only test already in this
suite (`PostgresConcurrencyTests`).

Pure-logic unit tests (run for real, no external dependency):

- **Template escaping/sanitization** (`EmailTemplateRendererTests`, CM-06): token-value
  HTML-escaping prevents merge-data injection; `HtmlSanitizer` strips `<script>`/`onerror=`
  from administrator-authored template HTML itself; unresolved tokens stay visible.
- **Secret masking/rotation** (`EmailCredentialProtectorTests`, CM-11): round-trip
  protect/unprotect, tamper detection (`CryptographicException` on modified ciphertext),
  masking never exposes more than the last 4 characters, and — the actual isolation mechanism
  CM-03 relies on — two different Data Protection purpose strings genuinely cannot decrypt
  each other's ciphertext.
- **Provider error classification** (`SmtpEmailProviderClient`/`BrevoApiEmailProviderClient`,
  CM-03): permanent vs. transient failure classification is unit-testable in principle but
  requires a live SMTP/HTTP endpoint to exercise meaningfully — not run here (see "not
  exercised" below); the classification logic itself was code-reviewed instead.
- **State transitions** (`EmailQueueStateMachineTests`, CM-02/CM-09): every documented legal
  transition and a representative set of illegal ones (including the DeadLetter→Pending
  transition CM-09's restore action needed).
- **Retry timing** (`EmailRetryBackoffCalculatorTests`, CM-04): exponential growth, jitter
  bounds, never-negative, never-exceeds-max.
- **Idempotency** (`EmailIdempotencyKeyBuilderTests`, CM-02): deterministic for identical
  input, differs when any component changes, normalizes recipient case/whitespace.
- **Recipient resolution**: `CommunicationRecipientResolver` (CM-07) is DB-backed
  (queries `db.Users` through `WorkspaceScope`) — not unit-testable without a database;
  code-reviewed instead (see Security review below).
- **Tenant filtering**: exercised indirectly via `EmailProviderResolutionServiceTests`
  (`[PostgresFact]`, CM-03 — Store→Company→Global precedence). `CommunicationRecipientResolver`
  and every `WorkspaceScope.ApplyTo` call site across `CommunicationEndpoints.cs`/
  `EmailSuppressionEndpoints.cs` were code-reviewed rather than integration-tested, consistent
  with this repo's established pattern of not writing integration tests directly against
  minimal-API endpoint handlers (confirmed during CM-01 research — no existing module in this
  codebase does this either).
- **Suppression**: `EmailSuppressionEndpoints`/`BrevoWebhookEndpoints`'s suppression-creation
  logic is DB-backed; code-reviewed (see below) rather than integration-tested.
- **Webhook mapping/deduplication/forgery** (`BrevoWebhookAuthenticatorTests`, CM-09): 8 tests
  covering fail-closed defaults (disabled, and enabled-with-no-credentials), correct/incorrect
  Basic Auth, correct/incorrect header token, and CIDR allow-list accept/reject — all run for
  real, not Postgres-gated. Event-type mapping/dedup-key-building
  (`BrevoWebhookEndpoints.ProcessEventAsync`) is DB-backed and was code-reviewed.
- **Attachment authorization**: `CommunicationAttachmentStorageServiceTests` (CM-11) covers
  the pure validation layer (extension allow-list including case-insensitivity, size
  cap boundaries, zero/negative rejection, storage-root fallback). The actual per-request
  re-authorization check (sender-or-recipient) in `CommunicationEndpoints.DownloadAttachmentAsync`
  is DB-backed and was code-reviewed.
- **SMTP presets**: `SmtpPresetCatalogTests` (CM-03).

## Integration/concurrency tests — Postgres-gated, skipped in this sandbox

All in `backend/Garmetix.Api.Tests/Communication/`, require `GARMETIX_TEST_POSTGRES`:

- `EmailQueueConcurrencyTests`: duplicate-IdempotencyKey concurrent insert (only one wins),
  stale-Revision concurrent save (`DbUpdateConcurrencyException`).
- `EmailQueueClaimConcurrencyTests`: 4 simulated workers racing to claim 12 seeded items never
  double-claim (`FOR UPDATE SKIP LOCKED`), both stale-lease-recovery outcomes.
- `EmailProviderResolutionServiceTests`: Store→Company→Global→LocalMasterOnly precedence.

These were written and reviewed for correctness but **not executed against a real Postgres
instance in this sandbox** — no `GARMETIX_TEST_POSTGRES` connection string was available here,
matching this project's established limitation on every prior Postgres-gated test in this
suite.

## Security review (manual code review, not automated)

Performed as part of CM-11, checked against `docs/communication-mail-security.md`'s checklist:

- **Credentials**: `GetCredentialsAsync`/`GetProviderAsync` never return `EncryptedValue`,
  only `MaskedDisplayValue`. `SaveCredentialsAsync` never re-encrypts a blank submitted value
  (retain-on-blank, rotate-on-value). Encrypted at rest via `EmailCredentialProtector`
  (ASP.NET Core Data Protection, authenticated encryption).
- **Header injection**: `SmtpEmailProviderClient.BuildMimeMessage` strips CR/LF from Subject
  before handing it to MimeMessage; MimeKit's own header encoding (RFC 2047) further prevents
  injection through From/To/Cc display names.
- **Webhook forgery**: covered by the 8 `BrevoWebhookAuthenticatorTests` above — fails closed,
  constant-time credential comparison (`CryptographicOperations.FixedTimeEquals`).
- **Webhook rate/size limits**: `MaxBodyBytes` (256 KB default) enforced before JSON parsing;
  a dedicated ASP.NET Core `AddFixedWindowLimiter("brevo-webhook", ...)` (120 req/min, added
  during this CM-11 pass after the review found it missing) scoped only to the webhook route
  — never affects normal user-facing API traffic.
- **Tenant/cross-tenant isolation**: every read/write in `CommunicationEndpoints.cs`,
  `EmailSuppressionEndpoints.cs`, `EmailProviderEndpoints.cs` either goes through
  `WorkspaceScope.ApplyTo`/`CanWrite` or (for internal-message conversations, which have no
  single "tenant" concept) an explicit sender-or-recipient membership check
  (`GetConversationAsync`, `DownloadAttachmentAsync`, `SetFolderStateAsync`, `ReplyAsync`) —
  confirmed by reading every handler, not assumed.
- **Confidential HR/Payroll access**: the new `/payslips/{id}/send-email` endpoint sits inside
  `MapPayrollEndpoints`'s existing route group, which requires `GarmetixPolicies.Payroll` at
  the group level — inherited automatically, not re-implemented.
- **ID enumeration**: every `{id:guid}` lookup returns a generic 404 ("not found") rather than
  distinguishing "doesn't exist" from "exists but not yours" — checked across all new
  endpoints.
- **Attachment abuse**: extension allow-list + 15MB cap + SHA-256 checksum + random
  non-guessable stored file names (`{Guid.NewGuid():N}{extension}`, original name kept only as
  a DB column) in `CommunicationAttachmentStorageService`.
- **SQL injection**: every query is EF Core LINQ except the one intentional raw-SQL claim
  query in `EmailQueueClaimService` (`FromSqlInterpolated`, which Npgsql parameterizes
  correctly — `{batchSize}`/`{leaseUntil}`/`{workerOwner}`/`{claimableStatuses}` are bound
  parameters, not string-concatenated).
- **Secret leakage in logs**: `EmailQueueItemProcessor`/`EmailDeliveryAttempt` never log or
  store a request's Authorization header or the raw provider request body — only sanitized
  error codes/messages. `BrevoWebhookEndpoints.SanitizeForStorage` stores only
  `Event`/`Email`/`MessageId`/`Reason`/`Subject` from the webhook payload, never the full raw
  body.

## Not exercised (disclosed, not silently skipped)

- **Live Brevo send/webhook round-trip**: no Brevo account/API key/webhook credentials exist
  in this sandbox. `BrevoApiEmailProviderClient`/`SmtpEmailProviderClient` are built directly
  from Brevo's documented v3 API shape and MailKit's SMTP client respectively, but neither has
  been called against a real Brevo account.
- **Live browser click-through** of the Communication & Mail app (compose, reply, provider
  test-send, template preview, queue retry): this sandbox has no Garmetix login credentials,
  consistent with every other Books/HR/Purchase stage documented in this project's history.
  Verification here relied on clean production builds (`nuxt generate`, all routes prerender
  with real content confirmed via compiled-bundle grep) rather than interactive testing.
- **Provider outage while business posting succeeds**: architecturally guaranteed by
  construction (every CM-08 integration point is a new endpoint called *after* the business
  transaction already committed — see `docs/communication-mail-architecture.md`), but not
  exercised as a live failure-injection test.
- **Multi-worker load test at scale**: `EmailQueueClaimConcurrencyTests` proves correctness
  with 4 simulated workers against 12 rows; it does not stress-test the queue at production
  volume.

## Manual acceptance checklist (from the original 12-item plan)

| # | Item | Status |
|---|------|--------|
| 1 | Create a Brevo provider, verify the saved key cannot be read back | Endpoint-verified via code review (`GetCredentialsAsync` never returns `EncryptedValue`); not exercised against a live Brevo key |
| 2 | Test connection, queue a test email | `EmailProviderTestService` built and wired; not exercised live |
| 3 | Confirm worker sending, provider message ID, delivery timeline | `EmailQueueWorker`/`EmailQueueItemProcessor` built, unit-tested for claim/backoff logic; not exercised against a live provider |
| 4 | Simulate provider outage; invoice posts while email retries | Guaranteed by the CM-08 additive-endpoint architecture; not live-injected |
| 5 | Permanent failure reaches DeadLetter | Covered by `EmailQueueStateMachineTests` transition logic; not live-exercised |
| 6 | Hard bounce creates scoped suppression | `BrevoWebhookEndpoints.UpsertSuppressionAsync` built and code-reviewed; not live-exercised |
| 7 | Remove suppression with required reason, verify audit | `EmailSuppressionEndpoints.RemoveAsync` requires and stores a reason; not live-exercised |
| 8 | Internal message, per-recipient unread/read status | `CommunicationEndpoints` built with auto-mark-read-on-open; not live-exercised |
| 9 | Unrelated user/tenant cannot access message or attachment | Verified by code review of every access-check call site (see Security review) |
| 10 | HR/Payroll confidentiality enforced | Verified — new endpoint inherits the existing route group's `GarmetixPolicies.Payroll` |
| 11 | Resend creates a new linked record | `EmailEnqueueService.ResendAsync` always creates a new row with `ResentFromQueueItemId` set, never mutates the original |
| 12 | All `/communication` pages use the normal layout/navigation | Verified — every page wraps in the same `ModularAppShell` every other modular app uses |

Items without a live exercise are architecturally sound and code-reviewed, but require a real
Brevo account and a logged-in browser session to fully close out — both unavailable in this
sandbox.

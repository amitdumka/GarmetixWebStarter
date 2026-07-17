# Communication & Mail — Architecture (Reconciled With Actual Repo Conventions)

Stage: CM-01 (Discovery and design). This document records what the implementation
pack (`Garmetix-Mail-Com/`) asked for, what the repository actually does today, and
the concrete adaptation decided for each area before any Communication & Mail code
is written. Branch: `Mail-Com`. Not merged, not deployed.

## 1. Tenancy / scoping

The repo has no DbContext-level tenant filter. Scoping is applied explicitly per
endpoint via the static helper `backend/Garmetix.Api/Workspace/WorkspaceScope.cs`:
`ApplyTo<T>(IQueryable<T>, HttpContext)` (reflection over `CompanyId`/`StoreGroupId`/
`StoreId` properties), `ApplyDefaults(entity, context)`, `CanWrite(entity, context, out message)`,
`HasFullAccess(...)`.

**Decision**: every new Communication/Email entity gets nullable `CompanyId`/
`StoreGroupId`/`StoreId` (`Guid?`) properties and every endpoint calls
`WorkspaceScope.ApplyTo` on reads and `WorkspaceScope.CanWrite`/`ApplyDefaults` on
writes — no new scoping infrastructure.

## 2. Auth & permissions

`GarmetixPolicies.cs` is a flat list of policy name constants. `AccessPermissionMatrix.cs`
maps each to allowed `LoginRole`s (`ModuleRoles`) and `CanAccessPolicy` grants
Admin/Owner everything first. Normal policies are wired with one
`AddMatrixPolicy(options, GarmetixPolicies.X)` line in `Program.cs`. A policy needing
different logic than "any of these roles" is registered as its own
`options.AddPolicy(name, policy => policy.RequireAssertion(...))`, bypassing the
matrix (precedent: `GarmetixPolicies.SuperAdmin`, `GarmetixPolicies.SwalekhaOwner`).

**Decision** — new policy constants in `GarmetixPolicies.cs`, all matrix-registered
(no bypass policy needed, general communication access is role-based like every
other module):
- `Communication` — base module access (personal inbox, compose internal, view own).
- `CommunicationBroadcast` — store/company-wide broadcast and external email sending.
- `CommunicationTemplates` — template CRUD/versioning/approval.
- `CommunicationProviders` — provider/credential CRUD, Admin/Owner-tier only in `ModuleRoles`.
- `CommunicationQueue` — queue/log inspection, retry, cancel, reschedule, dead-letter restore.
- `CommunicationSuppression` — suppression list management/removal.

Add all six to `AccessPermissionMatrix.AllModules`/`ModuleRoles` and the relevant
role `Profiles` (Accountant/HR/StoreManager get `Communication` only; Admin/Owner/
PowerUser get all six — refined per-role in CM-05/CM-07 once endpoints exist).
HR/Payroll message content (payslip notices, attendance) stays gated behind the
existing HR/Payroll policies at the source-module endpoint, not just `Communication`
— `Communication` grants seeing your own inbox, not seeing payroll data that flows
through it.

## 3. Database

One shared `GarmetixDbContext` (`backend/Garmetix.Infrastructure/Data/GarmetixDbContext.cs`)
holds nearly every domain's `DbSet<T>`s. `SwalekhaDbContext` is a deliberate, one-off
exception for a fully isolated module. Communication & Mail integrates across Sales/
Purchase/HR/Accounting/Inventory/Admin, so it is **not** a Swalekha-style exception.

**Decision**: add all new entities as `DbSet<T>` on `GarmetixDbContext` with fluent
config in `OnModelCreating`, same file. Migrations here are hand-written, not
`dotnet ef`-generated (`PendingModelChangesWarning` is suppressed by design — see
`DependencyInjection.cs`); the SRP host runs `Database:SchemaBootstrapMode=FreshBaseline`
so migration files are not what actually provisions tables in production —
`DatabaseSchemaRepairService.cs`'s idempotent `CREATE TABLE IF NOT EXISTS`/
`ALTER TABLE ... ADD COLUMN IF NOT EXISTS` blocks are what matters. Plan: one hand-written
migration file (for local/dev `MigrateAsync` paths and documentation) **and** a new
`RepairCommunicationStorageAsync` method in `DatabaseSchemaRepairService.cs` wired into
the existing repair chain, plus a hand-updated `GarmetixDbContextModelSnapshot.cs`.
Per `docs/database-stage-backup-protocol.md`, a `--stage=` backup is required before
any schema-affecting stage — this project only has a local dev Postgres in this
sandbox, so the backup command is documented per stage but the actual SRP-host backup
step is Amit's to run before any real deploy (deploy is out of scope for this branch
regardless).

## 4. Encrypted credentials

`GstCredentialProtector` (`backend/Garmetix.Api/GstTax/GstCredentialProtector.cs`)
wraps a single process-wide `AddDataProtection().PersistKeysToFileSystem(...)`
registration (`Program.cs`) with a purpose string
`"Garmetix.GstTax.ProviderCredentials.v1"`. Data Protection key rings are meant to be
one per app, not one per module — a second `AddDataProtection()` call would fight the
first, not add isolation.

**Decision**: add a sibling `EmailCredentialProtector` class
(`backend/Garmetix.Api/Communication/EmailCredentialProtector.cs`) with its own purpose
string `"Garmetix.Communication.ProviderCredentials.v1"`, registered as
`AddSingleton<EmailCredentialProtector>()` against the *same* existing
`IDataProtectionProvider`/key-persistence path — no new `Gst:DataProtectionKeyPath`-style
config key needed, no new key folder. Same `Protect`/`Unprotect`/`Mask` API shape.

## 5. Background worker / queue

Existing `BackgroundService` precedents (`DotMatrixPrintWorker`,
`OracleSecondarySyncHostedService`, `PayrollAutomationHostedService`,
`BackupAutomationHostedService`) all use `IOptionsMonitor<TOptions>` (live-reloadable)
+ `PeriodicTimer` + a `RunSafelyAsync` wrapper that swallows `OperationCanceledException`
and logs everything else. There is no generic Outbox abstraction in this repo — every
module hand-writes its own claim/retry table (e.g. `OracleSecondarySyncLocalStore`'s
dead-letter table with `RetryCount`/`Resolved` columns, updated via raw SQL).

**Decision**: `EmailQueueWorker : BackgroundService` in
`backend/Garmetix.Api/Communication/EmailQueueWorker.cs`, same shape as
`OracleSecondarySyncHostedService` (`IOptionsMonitor<EmailQueueOptions>`,
`PeriodicTimer`, bounded batch per tick, `RunSafelyAsync`). Atomic claim uses
`FOR UPDATE SKIP LOCKED` raw SQL against `EmailQueueItems` (Postgres-safe multi-worker
claim), tested with a `[PostgresFact]`-gated concurrency test mirroring
`backend/Garmetix.Api.Tests/Infrastructure/PostgresConcurrencyTests.cs`'s
`Task.WhenAll`-of-N-DbContexts pattern (skipped without `GARMETIX_TEST_POSTGRES` set,
same as every other Postgres-only test in this repo).

## 6. Email sending — existing code, and the new abstraction

`IEmailSender`/`SmtpEmailSender`/`EmailOptions`/`PasswordResetEmailService`
(`backend/Garmetix.Api/Auth/`) already exist: a single global SMTP account
(`appsettings.json` `"Email"` section, currently `Enabled: false`), sending via
`System.Net.Mail.SmtpClient`, used only by password reset.

**Decision, explicit scope boundary**: leave `IEmailSender`/`SmtpEmailSender`/
`EmailOptions`/`PasswordResetEmailService` **completely untouched**. Password reset
keeps working exactly as today, zero regression risk, and the master prompt's own
architecture principle ("business transactions never wait for provider delivery")
doesn't really apply to a synchronous one-off reset email the same way it applies to
Sales/Purchase/Payroll posting — rewiring it into a new async queue is out of scope
and not requested. The new Communication & Mail module builds a parallel, richer
provider layer for its own queue, following the existing `IAssistantModelClient`/
`AssistantModelClientFactory` shape (`backend/Garmetix.Api/Assistant/AssistantModelClient.cs`):
- `ITransactionalEmailProviderClient` — `SendAsync(EmailSendRequest, CancellationToken)`
  returning a provider-neutral result (message id, status, raw-response-for-logging).
- `BrevoApiEmailProviderClient` — Brevo transactional email HTTP API.
- `SmtpEmailProviderClient` — one reusable SMTP implementation (MailKit, for STARTTLS/
  SSL/auth correctness and testability that `System.Net.Mail` lacks), driven by presets
  (Brevo SMTP, GoDaddy Professional Email, Microsoft 365, Gmail, custom, local Postfix)
  that only supply default host/port/TLS values — one implementation, not one per preset.
- `EmailProviderClientFactory` — resolves the right client per `EmailProviderConfiguration`
  row (scope-resolved: Store → Company → Tenant default → `LocalMasterOnly`-style
  disabled fallback that queues but never sends, mirroring the GST module's
  `LocalMasterOnly` provider concept for "module works with zero external config").

A future stage *could* offer to point `PasswordResetEmailService` at the new queue,
but that is not part of this pack's request and is called out as a follow-up, not
silently done.

## 7. File / attachment storage

Consistent pattern: a configurable `X:StorageRoot`/`X:StoragePath` key falling back to
`Path.Combine(environment.ContentRootPath, "data", "x")`, per-owner subfolder layout,
extension allow-list + size limit enforced before write, auth-gated
`Results.File(...)` download endpoint with the `RequireAuthorization` at the route
**group** level (`PurchaseInvoiceImportService.cs`, `SwalekhaDocumentEndpoints.cs`).

**Decision**: `Communication:AttachmentStorage:StoragePath` config key (fallback
`data/communication-attachments`), layout
`{companyId:N}/{yyyy}/{MM}/{conversationOrQueueItemId:N}/{storedFileName}`, random
non-guessable stored file names (original name kept only as a DB column), checksum
(SHA-256) stored per attachment, extension allow-list + MIME sniff + size/count caps
enforced in a shared `CommunicationAttachmentStorageService` used by both internal
messages and outbound mail attachments. Download via
`GET /api/communication/attachments/{id}/download`, group-gated by `Communication`
policy plus a per-attachment `WorkspaceScope`/source-record authorization re-check
(never trust the URL alone).

## 8. Logging & audit

Standard `ILogger<T>` everywhere (no Serilog). `AuditLogEntries`
(`GarmetixDbContext.cs`) + `AuditActorContext`/`AuditActorMiddleware`
(`backend/Garmetix.Infrastructure/Audit/`, `backend/Garmetix.Api/Audit/`) is the
existing audit mechanism, populated per-request. A separate
`ApplicationMessageLogService` (`backend/Garmetix.Api/Messages/`) backs the
per-module "message/event log" pages already duplicated into `books`/`admin`
(`message-logs.vue` in both).

**Decision**: write provider/credential changes, template edits, retries/cancellations,
suppression add/remove, and sensitive-attachment access into the existing
`AuditLogEntries` table via the existing audit mechanism — no new audit table.
Delivery attempts/events (high-volume, append-only, not really "user actions") get
their own `EmailDeliveryAttempt`/`EmailDeliveryEvent` tables per the pack's data model
instead of going through `AuditLogEntries` — different retention/volume profile,
same reasoning `OracleSecondarySyncLocalStore` already applies to its own sync-log table.

## 9. Frontend placement

Two existing patterns: an isolated module gets its own Nuxt app deliberately excluded
from the switcher (Swalekha — own `nuxt.config.ts`, own `auth.global.ts`, not in
`GarmetixFrontendId`); a cross-cutting concern touching many existing apps is
currently handled by literally duplicating a small page into each relevant app
(`message-logs.vue` exists separately in `books` and `admin`).

Communication & Mail needs ~14 routes (dashboard/inbox/sent/drafts/archive/trash/
compose/conversation/queue/log/templates/providers/suppression/settings) under one
`/communication` path and must be reachable from Sales(main)/Purchase(main)/HR/
Accounting(books)/Inventory/Admin — too large a surface to duplicate per app like
`message-logs.vue`, and it needs its own real app shell (unread badge, its own nav),
unlike Swalekha it must **not** be excluded from the switcher.

**Decision**: new dedicated Nuxt app `frontend/modular/apps/communication`
(`GarmetixFrontendId = 'communication'`, added to `apps.ts`, port `3110` — the next
free port after `inventory` 3107/`final-accounts` 3108/`swalekha` 3109 (Swalekha's
port is not in `apps.ts` since it's excluded from the switcher registry)),
`envUrlKey: NUXT_PUBLIC_GARMETIX_COMMUNICATION_URL`, registered in the app switcher
like `books`/`crm`/`inventory` (not excluded like Swalekha). Routes registered in
`frontend/modular/config/routes.ts` with `targetApp: 'communication'`. Every other
app's `ModularAppShell.vue` `localMenus` gets a lightweight "Communication" nav
group/link (mirroring how `message-logs` cross-links today) pointing at the
dedicated app rather than duplicating pages — plus a small unread-count badge
fetched from a shared API call, similar to the sparkle/Assistant launcher pattern
already used for the cross-app Assistant entry point.

## 10. Deployment (documented, not executed on this branch)

No root `docker-compose.yml` — only `legacy/docker-compose*.yml` (legacy stack).
Current deployment is the SRP shell-script pipeline
(`frontend/modular/deploy/srp-whole-site-deploy.sh`, per-module sibling scripts like
`srp-swalekha-deploy.sh`). Per `docs/database-stage-backup-protocol.md`, any
DB-affecting stage requires
`npm --prefix frontend/modular run deploy:srp:backup -- --stage=<Name>` before it, on
the real deployed host. This branch does not push, merge, deploy, touch DNS, or run
that backup command against SRP — CM-12 will document the exact commands Amit needs
to run when he approves a deploy, matching `srp-swalekha-deploy.sh`'s shape with a new
`srp-communication-deploy.sh`.

The optional Postfix relay (CM-10) stays in isolated, disabled-by-default deploy
files only — never wired into the main Compose/deploy path automatically.

## 11. Testing

Backend tests: `backend/Garmetix.Api.Tests/`, xUnit, module-mirroring subfolders. Pure
calculator-style logic gets plain `[Fact]` tests (Swalekha's XIRR/loan-calculator
tests as the style precedent). Concurrency/Postgres-only tests use `[PostgresFact]`
(`PostgresFactAttribute.cs`), skipped without `GARMETIX_TEST_POSTGRES` — consistent
with every other Postgres-gated test already in this suite (matches the "3
pre-existing Postgres-only skipped" line seen throughout `CLAUDE.md`'s history).

New tests: `backend/Garmetix.Api.Tests/Communication/` — template variable escaping,
idempotency-key generation, backoff/jitter calculation, queue state-transition
legality (pure logic, no DB) as `[Fact]`s; atomic multi-worker claim as `[PostgresFact]`.

## Summary of concrete new surfaces

- Backend: `backend/Garmetix.Api/Communication/*` (endpoints, services, worker,
  provider clients, credential protector), `backend/Garmetix.Domain/Generated/Models/Communication/*`
  (entities), `backend/Garmetix.Api.Tests/Communication/*`.
- Frontend: `frontend/modular/apps/communication/*` (new app), small nav/link edits
  in each existing app's `ModularAppShell.vue` + `routes.ts`/`apps.ts` registration.
- Docs: this file, plus `docs/communication-mail-deployment.md`/
  `communication-mail-testing.md`/`communication-mail-security.md` (already in the
  implementation pack, retained as reference and refined per stage).
- Nothing removed or rewired: `IEmailSender`/`SmtpEmailSender`/`PasswordResetEmailService`
  untouched; `GarmetixDbContext` gains new `DbSet`s only; no existing endpoint,
  policy, or frontend route is changed in a breaking way.

# Garmetix Latest Roadmap And TODO - August 2026

Generated: 2026-08-20 IST  
Workspace: `C:\AIArea\Codex\GarmetixWebStarter`  
Branch: `version7`  
GitHub state fetched: `origin/version7` fast-forwarded to `39ce892e feat(billing): Sale Return / Exchange register (Back Office + POS)`

## Current Source Snapshot

- Local `version7` is synced with `origin/version7` at `39ce892e`.
- GitHub fetch also found a newer side branch: `origin/android-app` at `335e242e Android App change`. This roadmap does not merge or judge that branch; it needs a separate review before any adoption.
- GitHub issue API check found no open standalone issues. The returned issue list is closed pull requests only:
  - PR #6 `Mail com`, merged 2026-07-18.
  - PR #5 `Balancesheet`, merged 2026-07-14.
  - PRs #1-#4 are older closed release/merge PRs.
- `gh` CLI is not installed in this workspace, so issue state was checked through the GitHub API.

## Non-Negotiable Rules

- Before any implementation stage, deploy, schema repair, live backfill, accounting mutation, service restart tied to DB work, or data correction that can affect PostgreSQL, create a restore-ready SRP backup first.
- Use the project backup protocol: `npm --prefix frontend/modular run deploy:srp:backup -- --stage=<StageName>`.
- Do not purge pre-July sale invoices. They are approved imported data and must be retained.
- Do not run COA normalization, party/ledger relink, party merge, ledger merge, report-source switch, production restore, or fresh source mutation without Amit/CA approval and a new named backup.
- Current SRP target must be verified before deploy. Recent August evidence prefers `192.168.11.94`; older docs/scripts may still mention `192.168.11.127`.
- For SRP completion, verify live content and API routes, not only command exit codes or HTTP status.

## P0 - Final Accounts / Balance Sheet August Closure

Status from the latest August evidence note:

- BS-19 official endpoint had `Pending: 0`, `Drift: 0`, Inventory difference `0.00`.
- Sales difference `-125,186.05` was accepted by Amit as discount plus debit round-off presentation.
- BS-20 still had blockers: `10` statement differences and `78` active exception mappings.
- BS-21 restore drill passed only when explicitly pointed at the Garmetix backup, not the same-timestamp Swalekha dump.

Important freshness note:

- `.codex/codexpending-aug.md` is current through the BS-20 classifier deploy on 2026-08-04.
- The fetched GitHub tip now includes later Aug 7 billing/credit-note/sale-return-register work. Before any CA decision, refresh read-only BS-16 through BS-20 evidence again from the current deployed build/data.

TODO:

- [ ] Confirm which SRP host is authoritative now: `192.168.11.94`, `192.168.12.8`, or `192.168.11.127`.
- [ ] Confirm whether `39ce892e` is already deployed on the authoritative SRP host.
- [ ] Take a fresh named backup before any new accounting evidence capture if the run touches deployment or service state.
- [ ] Refresh BS-16 through BS-20 read-only evidence after the latest billing/import commits.
- [ ] Prepare a BS-20 statement-difference explanation for Amit/CA.
- [ ] Review the `78` active exception mappings and classify them as keep, replace, or remove-after-approval.
- [ ] Keep Purchase payment difference as expected until Amit enters purchase payments; do not auto-create purchase payments.
- [ ] Keep the `2` zero purchase stock rows unchanged until Amit rectifies purchase invoices.
- [ ] After Amit/CA approval, decide whether to proceed to COA normalization, party/ledger relink, Final Accounts report-source switch, or no further mutation.

## P0 - Deployment And Live Evidence Hygiene

- [ ] Standardize current SRP deploy configuration so scripts do not silently target the old `.127` host when the active office host is `.94`.
- [ ] For each deploy, capture:
  - backup filename and checksum,
  - release path,
  - API health,
  - app-shell byte/content checks,
  - authenticated or auth-gated route checks,
  - public Cloudflare check where available.
- [ ] Re-check that release-created `api/data` and backup files are readable/writable by the service user after deploy.
- [ ] Keep the Backup Maintenance UI pointed at durable `/opt/garmetix/backup/database`, not a release-rotated folder.

## P1 - Communication And Mail

Current state:

- Communication app and backend are now present in `version7`.
- PR #6 `Mail com` is merged.
- Existing TODOs still mark several completion gaps or live-validation gaps.

TODO:

- [ ] Confirm Communication app is deployed live and reachable from the app switcher.
- [ ] Add or verify queue health, usage metrics and admin dashboard coverage for CM-05.
- [ ] Wire preference-driven external notifications for internal direct/broadcast messages.
- [ ] Run cross-tenant/company/store authorization QA for mailbox, attachments, providers, templates, queue and suppression.
- [ ] Audit all business email send entry points and add missing visible frontend buttons where backend endpoints exist but UI actions are absent.
- [ ] Provision/test a real Brevo or SMTP provider:
  - domain/DKIM/SPF/DMARC,
  - real test send,
  - webhook receipt,
  - bounce/suppression timeline.
- [ ] Document production rollback and disable switches after the first real live email test.

## P1 - Admin, SaaS, Backup, Google Drive

Current state:

- Client Onboarding, Setup CRUD, SaaS Manager, subscription self-service, Data Consistency, Backup Maintenance, Import/Export actions and Message Logs pagination were built and pushed.
- Google Drive per-company OAuth is additive but requires external Google Cloud setup before it can actually connect.

TODO:

- [ ] Create a real Google Cloud OAuth Web application client.
- [ ] Configure:
  - `GoogleOAuthOptions:ClientId`,
  - `GoogleOAuthOptions:ClientSecret`,
  - `GoogleOAuthOptions:RedirectUri`,
  - `GoogleOAuthOptions:FrontendReturnUrl`.
- [ ] Use redirect URI `https://srp.aadwikafashion.in/api/backups/drive/oauth/callback` unless deployment hostname changes.
- [ ] Live-test one company connecting its own Drive account.
- [ ] Run a real company Drive backup and confirm the file lands in that company's Drive, not a shared server account.
- [ ] Live-test SaaS token activation, subscription expiry, trial quota and quota-blocking UX with real roles.
- [ ] Verify Backup Maintenance lists historical backups and can verify checksums after the latest release.
- [ ] Verify Data Consistency preview/apply actions are guarded and auditable before using apply on production data.

## P1 - Billing, POS, CRM And Customer-Facing Evidence

Current state:

- Latest GitHub tip adds Sale Return / Exchange register to Back Office and POS.
- Historical Vyapar Sale Return / Credit Note import landed 17 records and reclassified 4 matched payments to CreditNote.
- There was a disclosed POS browser-cache limitation during live verification of `/pos/returns-register`.

TODO:

- [ ] Verify `/billing/sale-returns` live with real data after at least one completed return/exchange exists.
- [ ] Verify `/pos/returns-register` in a clean browser/profile, not the cached testing browser.
- [ ] Create or identify one real return/exchange and confirm the register detail slideover opens the correct receipt.
- [ ] Reconcile the 17 imported historical credit notes:
  - 17 commercial notes,
  - 23 commercial note items,
  - 20 stock movements,
  - 4 payment reclassifications.
- [ ] Decide whether the harmless `SourceId` labeling mismatch on the 4 matched notes needs an admin correction tool or can remain as documented.
- [ ] Capture POS live operator evidence on 14-inch layout:
  - scanner flow,
  - sale save,
  - return,
  - exchange,
  - print queue recovery,
  - Epson/DotMatrix handoff.
- [ ] Capture CRM real public-token evidence:
  - customer opens `/i/:token`,
  - PDF download/print,
  - review link,
  - WhatsApp support,
  - private feedback,
  - banner click tracking.
- [ ] Decide whether POS history needs cashier/store filters, QR scan search and full Digital Bill activity timeline, or whether CRM remains the detail surface.

## P1 - Swalekha

Current state:

- Swalekha branch was marked final at `swalekha-final-v6.9.21`.
- All 15 PersonalFin stages are code-complete.
- Dedicated SRP deploy script exists.
- Module remains undeployed.

TODO:

- [ ] Decide whether to deploy Swalekha now or keep it parked.
- [ ] Before deploy, verify the script targets the current SRP host and backs up both `garmetix` and `swalekha_db`.
- [ ] Run `npm run modular:deploy:srp:swalekha -- --dry-run`.
- [ ] Run build-only validation for `swalekha-web` and shared API.
- [ ] Deploy only after a fresh backup and explicit approval.
- [ ] Live-test:
  - Owner-only access,
  - two-owner isolation,
  - account transaction reversal,
  - document upload/download/delete,
  - security self-check,
  - `swalekha_db` restore backup coverage.

## P2 - GST And Taxes Future Work

Current state:

- GST Stages GST-1 through GST-10 are code-complete and previously deployed.
- E-Invoice/E-Way Bill is intentionally provider-ready placeholder state, not live NIC/e-way portal integration.
- Dashboard note still says some dashboard-level sale/purchase GST rollups are not aggregated there.

TODO:

- [ ] Confirm current CBIC garment slabs, e-way threshold and state-specific rule assumptions with Amit/CA.
- [ ] Decide if live NIC e-invoice and e-way bill integration is required now.
- [ ] If yes, treat as a separate compliance project with official API credentials, sandbox, payload signing, QR/IRN lifecycle and cancellation flows.
- [ ] Add dashboard-level sale/purchase mismatch and monthly output/input GST rollups if Amit wants a single GST dashboard view.
- [ ] Live-test external GST provider configuration if a real provider account is available.

## P2 - Android Branch

Current state:

- Fetch discovered `origin/android-app` at `335e242e Android App change`.

TODO:

- [ ] Review `origin/android-app` in an isolated worktree before merge.
- [ ] Identify whether it is a separate mobile app, WebView wrapper, or partial experiment.
- [ ] Check for hardcoded URLs, secrets, signing files, unsafe API assumptions and deployment impact.
- [ ] Do not merge into `version7` without a dedicated review and build plan.

## P2 - Technical Debt And Known Incomplete Markers

Source markers still present:

- `backend/Garmetix.Domain/Generated/Models/Inventory/Inventory.cs`: Fabric/UOM support is still future work.
- `backend/Garmetix.Domain/Generated/Enums/BharatEnums.cs`: card type enum is marked incomplete.
- `backend/Garmetix.Domain/Generated/Models/Inventory/Invoicing.cs`: stored-vs-calculated invoice properties need cleanup/`JsonIgnore` review.
- `Inventory.cs` still contains an older Basic Rate Calculator TODO, but the POS helper modal reportedly closed the practical user-facing gap. Leave the source TODO only if a shared backend/toolkit version is still desired.

TODO:

- [ ] Decide whether Fabric inventory is in scope for Garmetix retail operations now.
- [ ] Expand card type/payment metadata only if payment reporting actually needs it.
- [ ] Review stored-vs-calculated invoice properties carefully before any schema or DTO cleanup.
- [ ] Remove or update stale TODO comments when user-facing replacements already exist.

## P3 - Documentation Cleanup

TODO:

- [ ] Update `.codex/todo.md` so it no longer says every BS-16 through BS-21 remote evidence item is still pending if the August evidence has superseded it.
- [ ] Update `.claude/roadmap.md` references that still call AntiGravity/SaaS work unreviewed, because Admin/SaaS was built on Aug 6-7.
- [ ] Keep old detailed evidence files, but make this `todoAugust2026.md` the latest roadmap index.
- [ ] After each new stage, update the stage-specific source doc and this August roadmap if priorities change.

## Recommended Next Sequence

1. Verify current deployment target and whether `39ce892e` is live.
2. Refresh Final Accounts BS-16 through BS-20 read-only evidence against the latest deployed data.
3. Prepare BS-20 statement-difference and exception-mapping explanation for Amit/CA.
4. Clean-browser verify POS `/returns-register` and Back Office `/billing/sale-returns`.
5. Decide with Amit whether the next live work is Final Accounts closure, Communication live email, Swalekha deploy, or Google Drive OAuth setup.

## Useful Commands

```powershell
git status --short --branch
git fetch --prune origin
git log --oneline --decorate --max-count=20
```

```powershell
npm --prefix frontend/modular run final-accounts:readiness
npm --prefix frontend/modular run validate
dotnet build backend/Garmetix.Api/Garmetix.Api.csproj -c Release
dotnet test backend/Garmetix.Api.Tests/Garmetix.Api.Tests.csproj -c Release
```

```powershell
npm --prefix frontend/modular run deploy:srp:backup -- --stage=<StageName>
npm --prefix frontend/modular run deploy:srp:restore-drill -- --confirm-non-production-restore --stage=BS21RestoreDrillProductionSafety
```


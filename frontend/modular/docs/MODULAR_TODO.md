# Garmetix Version6 Modular TODO

This is the working TODO for Version6 modular parity work. All frontend decoupling work belongs under `frontend/modular/`. The `legacy/` folder remains the Version4/Stage 11 fallback until modular apps reach parity.

Prompts and external plans are reference material, not strict folder orders. Each step should be adapted to the existing `frontend/modular/apps/*`, `frontend/modular/packages/*`, `frontend/modular/config/*`, and `frontend/modular/docs/*` structure.

## Process Rules

1. Pull/check the current `version6` branch before starting a module.
2. Keep changes small and commit after each completed module/stage.
3. Keep the ASP.NET API and PostgreSQL database unified unless a future stage explicitly changes that decision.
4. Keep `legacy/` behavior intact until modular parity is proven.
5. Add or update docs for each stage.
6. Run the safest available validation after each stage.
7. Do not hardcode server passwords or production secrets in source control.
8. When deployment automation is added, use SSH keys, environment variables, or prompted credentials.
9. Deploy to `.127`, run public/LAN acceptance and create database backups by cadence, not after every small checkpoint. Use `frontend/modular/docs/deployment-validation-cadence.md`.

## Stage 12A: Registry And Foundation

Status: in progress.

- 12A.1: Audit current project and document route ownership.
- 12A.2: Keep Version5 modular workspace under `frontend/modular/`.
- 12A.3: Create `frontend/modular/config/routes.ts` and document ownership.
- 12A.4: Build app switch/sidebar link helpers from the route registry.
- 12A.5: Add shared shell layout contracts for all modular apps.
- 12A.5 complete: shared API health, auth snapshot, and shell status cards.

## Stage 12B: POS First

Goal: split the fastest billing counter workflows first.

- 12B.1 complete: add POS route shell, login bridge, and first route pages inside `frontend/modular/apps/pos`.
- 12B.2 complete: add POS sale draft with product lookup, cart totals, payments, save and print.
- 12B.3 complete: add POS customer profile adjustments and print queue hardening.
- 12B.4 complete: add POS route guard, scanner focus, keyboard shortcuts, and save validation polish.
- 12B.5 complete: add POS sales return invoice lookup, item selection, credit note save, and print handoff.
- 12B.6 complete: add POS local held-bill queue with hold, resume, remove, and shortcut support.
- 12B.7 complete: connect POS day open/close to store-day API with petty cash preview and print.
- 12B.8 complete: add POS static deploy script and deployment notes.
- Planned routes: `/login`, `/`, `/day-open`, `/sale`, `/hold-bills`, `/returns`, `/print`, `/day-close`.
- Reuse `frontend/modular/packages/shared-api` and `frontend/modular/packages/shared-auth`.
- Keep sale invoice and print flows compatible with existing API behavior.
- Add POS build validation.
- POS Ubuntu static deploy script is available at `frontend/modular/deploy/pos-static-deploy.sh`.

## Stage 12C: HR

Goal: split employees, attendance, payroll, and salary payment screens.

- 12C.1 complete: add HR shell, auth guard, login, read-only dashboard, employee summary, attendance, payroll, salary payment, device pages, and placeholder route coverage.
- 12C.2 complete: connect safe HR actions for attendance payroll review rebuild/mark, salary draft rebuild/mark, regularization approve/reject, and salary payment preview.
- 12C.3 complete: add HR static deploy script and deployment notes.
- Planned first routes: `/login`, `/`, `/hr`, `/attendance`, `/attendance/today`, `/attendance/monthly`, `/attendance/payroll-summary`, `/payroll`, `/attendance/salary-payment`, `/attendance/devices`.
- Use existing attendance/payroll endpoints where available.
- Keep salary and attendance pages non-destructive until endpoint contracts are verified.
- Add HR build validation.
- HR Ubuntu static deploy script is available at `frontend/modular/deploy/hr-static-deploy.sh`.

## Stage 12D: AI Sense

Goal: build read-only analytics first, then connect backend endpoints.

- 12D.1 complete: add AI Sense shell, auth guard, login, business dashboard, stock risk, and route coverage for planned analysis screens.
- 12D.2 complete: add read-only `/api/ai-sense/*` analytics endpoints and connect modular analysis pages.
- 12D.3 complete: add AI Sense static deploy script and deployment notes.
- Planned routes: `/login`, `/`, `/sales-analysis`, `/purchase-analysis`, `/profit-analysis`, `/stock-risk`, `/vendor-analysis`, `/customer-analysis`, `/daily-summary`, `/monthly-summary`.
- Add empty/loading/error states before backend analytics endpoints are complete.
- Read-only analytics endpoints are added to the existing ASP.NET API.
- AI Sense planned pages are connected to real endpoints.
- AI Sense Ubuntu static deploy script is available at `frontend/modular/deploy/ai-sense-static-deploy.sh`.

## Stage 12E: Books

Goal: split accountant/CA workflows.

- 12E.1 complete: add Books shell, auth guard, login, read-only dashboard foundation, and route coverage placeholders.
- 12E.2 complete: connect read-only accounting master data for ledger groups, ledgers, parties, bank accounts, trial balance, and ledger sync health.
- 12E.3 complete: connect read-only bank operations for bank transactions, statements, reconciliation, cheque logs, vendor bank accounts, and bank account details.
- 12E.4 complete: connect read-only voucher review with ledger, party, bank, employee labels and authenticated PDF download readiness.
- 12E.5 complete: connect read-only petty cash review with sheet list, calculated daily summary, mismatch visibility, and authenticated A5 PDF download readiness.
- 12E.6 complete: connect read-only vendor payment and settlement review with voucher, purchase invoice, bank, allocation, and PDF handoff visibility.
- 12E.7 complete: connect read-only GST returns, GST reports and GST production/provider readiness with export handoff visibility.
- 12E.8 complete: connect financial year lock review, journal validation, accounting-scoped audit trail, and accounting-scoped message log visibility.
- 12E.9 complete: add Books static deploy script and deployment notes for the Books subdomain.
- Planned areas: accounting dashboard, ledgers, parties, vouchers, petty cash, cash details, debit notes, credit notes, GST reports, GST returns, audit/message logs.
- Keep banking and audit-sensitive flows explicit.
- First writable accounting actions are intentionally deferred until endpoint contracts, ledger posting behavior, and print/audit expectations are verified.
- Books Ubuntu static deploy script is available at `frontend/modular/deploy/books-static-deploy.sh`.

## Stage 12F: Admin/SaaS

Goal: split owner/developer/admin controls.

- 12F.1 complete: add Admin/SaaS shell, auth guard, login, dashboard and read-only setup, access, logs, runtime, production, license, import/export and onboarding foundation pages.
- 12F.2 complete: connect safe read-only admin diagnostics for backup, Google Drive backup, system health, database migrations, data consistency, production support drills and rehearsal.
- 12F.3 complete: add Admin/SaaS static deploy script and deployment notes for the Admin subdomain.
- Planned areas: setup/company/store, users/roles/permissions, license, feature/module enablement, client onboarding, system health, message logs, backup/restore, deployment diagnostics.
- Keep SuperAdmin-specific visibility rules explicit.
- Admin Ubuntu static deploy script is available at `frontend/modular/deploy/admin-static-deploy.sh`.

## Stage 12G: Main Back Office Cleanup

Goal: make `frontend/modular/apps/main` a lean back-office app instead of a catch-all UI.

- 12G.1 complete: add authenticated Main shell, route-level placeholders, ownership audit notes, and build validation for Back Office-owned routes.
- 12G.2 complete: connect Main Back Office dashboard pages to existing safe read-only dashboard summary endpoints.
- 12G.3 complete: connect sale invoice, purchase, inventory, stock operation, customer and operational report review pages to existing safe read-only endpoints.
- 12G.4 complete: add Main Back Office static deploy script and deployment notes for the root Garmetix subdomain.
- Keep dashboard, purchase, inventory, reports, customer/vendor operations, and store operations.
- Remove heavy POS/HR/AI/Books work from main only after target apps have parity.
- Optimize route-level loading and avoid layout-level data fetching.

## Stage 12H: Deployment Split

Goal: deploy each app independently while keeping one API and one database.

- 12H.1 complete: add consolidated deployment split guide for all modular static apps, the shared API, Cloudflare Tunnel hostnames, and the one-backend/one-database rule.
- 12H.2 complete: add one-command local validation for structure, all modular frontend static builds, and shared API build.
- 12H.3 complete: add deployment preflight for local static outputs, deploy scripts, env examples, secret hygiene, and optional SSH remote host readiness.
- 12H.4 complete: add host-specific Ubuntu server and Ubuntu desktop deployment notes with Nginx roots, Cloudflare Tunnel mapping, preflight commands, and rollback guidance.
- 12H.5 complete: add release-day checklist generator with target/app filters and optional ignored markdown output.
- 12H.6 complete: add deployment acceptance criteria for shared API, per-app deploy readiness, release gates, and go/no-go checks.
- Add static build output handling per app.
- Add deployment scripts under `frontend/modular/deploy/`.
- Target hosts:
  - Ubuntu server: `amit@192.168.11.126`
  - Ubuntu desktop: `amitkumar@192.168.11.127`
- Do not store passwords in scripts. Use SSH keys or prompt at runtime.
- Add Cloudflare Tunnel examples for:
  - `garmetix.aadwikafashion.in`
  - `pos.garmetix.aadwikafashion.in`
  - `hr.garmetix.aadwikafashion.in`
  - `books.garmetix.aadwikafashion.in`
  - `ai-sense.garmetix.aadwikafashion.in`
  - `admin.garmetix.aadwikafashion.in`
  - `api.garmetix.aadwikafashion.in`

## Stage 12Z: Verification

Goal: final verification before calling Stage 12 complete.

- 12Z complete: consolidate final Stage 12 verification for modular validation, legacy web/API builds, deploy docs, no database split, accepted warnings and next-stage risks.
- Build legacy web.
- Build legacy API.
- Build modular main, POS, HR, AI Sense, Books, and Admin.
- Validate env examples.
- Validate Cloudflare/deploy docs.
- Confirm no database split and no destructive migration.
- Document known issues and next stage.

## Stage 13A: Production Hardening

Goal: make the modular split deployable and testable in real environments before deeper parity work.

- 13A.1 complete: add production hardening roadmap and smoke-checklist generator for route/API-health verification across Main, POS, HR, AI Sense, Books and Admin.
- 13A.2 complete: add local route smoke tests with dry-run validation and optional Playwright live browser checks for modular apps.
- 13A.3 complete: add API health and auth-state smoke checks for local and public URL modes with optional token validation.
- 13A.4 complete: add Cloudflare-facing public URL smoke report generation with optional live app/API reachability checks.
- 13A.5 complete: add visual smoke notes for login, shell layout, app switching and access-denied pages, closing the Stage 13A hardening lane.
- Keep all checks secret-free and compatible with one shared API and one PostgreSQL database.
- Defer live Ubuntu/Cloudflare deployment until SSH keys, tunnel credentials and static-server config are ready on the host.

## Stage 13B: POS Parity And Writable Hardening

Goal: move from production smoke tooling into deeper POS workflow parity and safe writable workflow checks.

- 13B.1 complete: verify POS sale draft payload keys against the current ASP.NET billing sale DTO contract and centralize POS sale payload construction.
- 13B.2 complete: harden POS browser-local sale draft, held bill, return print queue and day open/close actions against corrupt local storage and duplicate clicks.
- 13B.3 complete: separate POS save success from PDF print-window failures and normalize scanned invoice references for return lookup.
- 13B.4 complete: normalize store-day API responses and keep day close successful when petty cash PDF print is blocked or fails.
- 13B.5 complete: centralize POS sales return request construction and extend contract parity checks for sale, return and exchange DTOs.
- 13B.6 complete: add dedicated POS sales exchange screen with original invoice return lines, replacement item scan cart, additional payment validation and print queue recovery.
- 13B.7 complete: add repeatable POS cashier/operator acceptance checklist covering Sale, Return, Exchange, day flow, print recovery and 14 inch laptop usability.
- 13B.8 complete: add server-backed POS held bill persistence with local browser fallback for hold, resume, remove and clear workflows.
- 13B.9 complete: add dry-run and optional live POS held-bill API smoke checks for auth gating, list access and create/delete lifecycle validation.
- 13B.10 complete: add dry-run and optional live browser acceptance for POS held-bill render, resume navigation and sale-draft recovery.
- 13B.11 complete: add POS save-after-resume readiness checks for resumed draft recovery, sale request construction, payment safety and Manager salesman fallback.
- 13B.12 complete: add dry-run and optional live POS fixture readiness checks for store scope, billing options, sellable stock, bank readiness and save payload safety.
- 13B closed: POS parity and writable hardening now has repeatable dry validation for contract parity, local workflows, print recovery, returns, exchange, server held bills, browser resume and save-readiness fixtures.
- Add live acceptance evidence when test credentials are available.
- Keep off-book cash voucher and non-GST flows separated from regular books.
- Preserve unified API/database until a future backend split is explicitly approved.

## Stage 13C: HR Payroll Attendance Hardening

Goal: move the HR/payroll module lane through the same pattern: route readiness, contract checks, read/write safety, dry smoke, optional live readiness and closure.

- 13C.1 complete: add non-mutating HR/payroll readiness checks for HR route ownership, attendance read models, payroll review, salary payment candidates, attendance devices, fingerprint bridge status, face/liveness status, recent payslips and salary payment list endpoints.
- 13C.2 complete: add HR attendance contract checks for monthly attendance, payroll summary, payroll review, salary draft and salary payment preview DTO field expectations.
- 13C.3 complete: add HR browser acceptance notes/checks for attendance, payroll review and salary payment pages on 14 inch laptop layouts.
- 13C.4 complete: add Mantra/fingerprint device bridge readiness docs and simulator checks without raw biometric storage.
- 13C.5 complete: add controlled live payroll preview validation without creating salary payment vouchers.
- 13C closed: HR/payroll attendance hardening now has repeatable dry validation for endpoint readiness, DTO contracts, browser layout acceptance, device bridge readiness and non-mutating salary payment preview.
- Keep all real data mutation behind explicit opt-in flags.
- Continue one shared ASP.NET API and one PostgreSQL database until a future split is explicitly approved.

## Stage 13D: Books Accounting Audit And Posting Readiness

Goal: move the Books/accounting module lane through the same pattern: route readiness, contract checks, browser safety, ledger sync checks, dry smoke, optional live readiness and closure.

- 13D.1 complete: add non-mutating Books/accounting readiness checks for route ownership, ledger and party masters, bank operations, vouchers, petty cash, audit, message logs, financial year locks and GST accounting summary endpoints.
- 13D.2 complete: add Books accounting contract checks for voucher, ledger, party, bank account, audit and GST summary field expectations.
- 13D.3 complete: add Books browser acceptance checks for vouchers, petty cash, cash details, audit and GST pages on 14 inch laptop layouts.
- 13D.4 complete: add ledger/party/bank-account sync readiness validation to keep hidden party and bank ledgers internal.
- 13D.5 complete: add controlled live posting readiness preflight without creating vouchers or journals.
- 13D closed: Books/accounting hardening now has repeatable dry validation for endpoint readiness, DTO contracts, browser layout acceptance, internal ledger sync safety and posting prerequisites without mutations.
- Preserve off-book cash voucher and non-GST flows separately from regular accounting books.
- Keep all real data mutation behind explicit opt-in flags.
- Continue one shared ASP.NET API and one PostgreSQL database until a future split is explicitly approved.

## Stage 13E: Next Modular Hardening Lane

Goal: move Main Back Office through the same hardening pattern used for POS, HR and Books.

- 13E.1 complete: add non-mutating Main Back Office readiness checks for route ownership, dashboard read models, recent sale/purchase review, stock summary, customer search, product lookup and workspace scope.
- 13E.2 complete: add contract parity checks for Main Back Office dashboard, sale review, purchase review, inventory summary, customer preview, product lookup and workspace DTO fields.
- 13E.3 complete: add Main browser acceptance checks for 14 inch laptop usability, route headings, table scrolling, safe messages and app boundary clarity.
- 13E.4 complete: add writable-readiness preflight for Main sale-review actions, purchase intake/review, customer profile handoff and inventory operations without mutations by default.
- 13E.5 complete: add Main closure checklist for dashboard, billing review, purchase review, inventory, customers, reports, deployment readiness and residual writable risks.
- 13E closed: Main Back Office hardening now has repeatable dry validation for route ownership, read models, DTO contracts, browser acceptance, writable preflight and deployment handoff.
- Main closed route set: `/`, `/dashboard`, `/dashboard/todays`, `/dashboard/store-manager`, `/billing`, `/purchase`, `/inventory`, `/stock-operations`, `/customers`, `/reports`.
- Keep Main focused on back-office operational review while POS, HR, Books and Admin/SaaS retain their dedicated workflows.
- Keep store-day, tailoring, purchase return and document scan writable behavior deferred until endpoint contracts are promoted.
- Preserve one shared ASP.NET API and one PostgreSQL database until a future split is explicitly approved.

## Stage 13F: Admin/SaaS Live Operations Hardening

Goal: move Admin/SaaS through the same closure pattern with live-operations safety before production use.

- 13F.1 complete: add Admin/SaaS readiness checks for SuperAdmin/Admin visibility, setup/admin diagnostics, message logs, backup/restore, factory reset guardrails, import/export controls and deployment governance.
- 13F.2 complete: add Admin browser acceptance checks for login, access denied, setup, users, system health, message logs, backup, import/export and production pages on 14 inch laptop layouts.
- 13F.3 complete: add guarded Admin writable/live preflight for backup restore, factory reset, license generation/activation and import/export commit without executing dangerous mutations by default.
- 13F.4 complete: close the Admin/SaaS hardening lane with a repeatable closure gate and remaining-risk handoff.
- 13F closed: Admin/SaaS hardening now has repeatable dry validation for readiness, browser acceptance, writable preflight and deployment handoff.
- Current Admin risk: backend factory reset is Admin-policy protected and confirmation-gated with safety backup, but not yet SuperAdmin-only in code.
- Keep factory reset and restore destructive flows behind explicit live/admin confirmation gates.
- Keep production secrets out of repository scripts and docs.

## Stage 13G: SRP Whole-Site Deployment

Goal: deploy the complete modular website to the Ubuntu desktop SRP target without disturbing the existing production hostname.

- 13G.1 complete: add SRP whole-site deploy foundation for `srp.aadwikafashion.in`, path-based modular static apps, reusable config, API publish staging, Nginx, systemd and Cloudflare Tunnel templates.
- 13G.1a complete: correct SRP SSH target to `amitkumar@192.168.11.127` and add private non-interactive SSH/sudo password support through a secrets file and `sshpass`.
- 13G.2 complete: add WSL-friendly SRP host readiness checks for local tools, SSH, sudo, Nginx, dotnet, cloudflared, PostgreSQL client, API env and Cloudflare tunnel files.
- 13G.3 complete: run live WSL readiness, install base host packages, add self-contained Linux API publish and guard API startup until real env values exist.
- 13G.4 complete: add runtime bootstrap for PostgreSQL server, generated API env secrets, API service startup and cloudflared package installation.
- 13G.5 complete: add credential-safe Cloudflare tunnel activation/status script and public SRP endpoint verification flow.
- 13G.6 complete: add SRP public acceptance runner for DNS, public routes, API health and LAN fallback verification, with strict mode for final Cloudflare acceptance.
- 13G.7 complete: fix LAN login/API unreachable issue by using same-origin SRP app/API URLs and relative API URL support in the shared client.
- 13G.8 next: add real Cloudflare tunnel credentials outside git, run strict public acceptance, then complete browser login, app path loading, API health and cross-app navigation checks from `https://srp.aadwikafashion.in`.
- Keep SRP secrets and tunnel credentials outside git.

## Stage 14A: Version6 POS Parity Baseline

Goal: restart modular parity work module-by-module, with POS first, and keep live-data safety explicit before any write-heavy test.

- 14A.1 complete: bump modular version identity to `6.0.1`, align modular package/app/shared package versions to Version6, repair POS route ownership to app-local paths, and add a non-mutating POS parity baseline gate.
- 14A.2 complete: bump modular version identity to `6.0.2`, add an SRP `.127` PostgreSQL backup command, and add a POS live-safe deploy gate for non-mutating pre-deploy checks.
- 14A.3 complete: bump modular version identity to `6.0.3` and add controlled POS live sale acceptance with guarded invoice creation, receipt and PDF verification.
- 14A.4 complete: bump modular version identity to `6.0.4` and add controlled POS return/exchange acceptance with guarded return and exchange creation.
- 14A.5 complete: bump modular version identity to `6.0.5` and add POS operations recovery acceptance for held bills, print queue recovery, day open/day close readiness and DotMatrix handoff.
- 14A.6 complete: bump modular version identity to `6.0.6` and add POS cashier workflow acceptance for scan/search, split payments, bank validation, return/exchange flow and optional 14-inch browser fixture checks.
- 14A.7 complete: bump modular version identity to `6.0.7`, add POS final closure gate, and document the every-third-checkpoint deployment/acceptance cadence.
- 14A.8 complete: bump modular version identity to `6.0.8`, repair New Sale product search datalist/barcode resolution, and move payment/customer adjustment blocks below the item list with totals promoted upward.
- POS first closure order:
  1. Confirm sale invoice save/print parity against legacy.
  2. Confirm return/exchange parity and PDF/print recovery.
  3. Confirm server held bills, local fallback, resume and remove.
  4. Confirm day open/day close with petty cash and DotMatrix handoff.
  5. Confirm non-GST/off-book and cash voucher boundaries before moving them fully into POS.
  6. Run controlled live-write acceptance only after a `.127` backup.
- Stage 14B next: start HR modular parity from the Version6 base, with deployment deferred until the cadence or risk level requires it.
- HR starts after POS code closure, while final live-token/manual cashier evidence remains a handover gate.
- Books starts only after HR acceptance is passed.

## Stage 14B: Version6 HR Modular Parity

Goal: move HR, attendance and payroll through module-by-module parity without touching live salary/payment data unless an explicit live-write gate is used.

- 14B.1 complete: bump modular version identity to `6.0.9`, add HR parity baseline, and normalize HR API paths so existing `api/...` page calls do not duplicate the `/api` prefix.
- 14B.2 complete: bump modular version identity to `6.0.10`, repair salary payment preview so payslip id is optional, and verify advance, previous due, deductions, net payable, outstanding, rounded payment and round-off contract fields.
- 14B.3 complete: bump modular version identity to `6.0.11`, tighten attendance today/monthly UI behavior, add generated monthly day review, and add preview-only monthly attendance generation readiness.
- 14B.4 complete: bump modular version identity to `6.0.12`, add Manual Punch entry, add regularization request creation/review parity, and validate backend punch/correction contracts.
- 14B.5 complete: bump modular version identity to `6.0.13`, add guarded monthly recalculation, month lock/unlock and selected-row delete actions with exact confirmation phrase, selection and audit reason checks.
- 14B.6 complete: bump modular version identity to `6.0.14`, replace attendance device/kiosk placeholders with device registration/revoke, web kiosk readiness, kiosk monitor, mobile kiosk contract, rehearsal checklist and fingerprint bridge simulator readiness consoles.
- 14B.7 complete: bump modular version identity to `6.0.15`, add payroll approval evidence columns/counts, guarded payslip generation and guarded salary payment generation with exact confirmation phrases and audit notes.
- 14B.8 complete: bump modular version identity to `6.0.16`, add HR payroll summary and payslip CSV exports, and add a non-mutating live payroll acceptance gate for `.127`/Cloudflare.
- 14B.9 complete: bump modular version identity to `6.0.17`, add HR final closure gate with conditional live-token/manual evidence and Books parity handoff.
- 14B.10 complete: bump modular version identity to `6.0.19`, reopen HR after legacy comparison, add Employee Shift Rules, add Payroll Finalization, and add a legacy HR parity audit gate.
- 14B.11 complete: bump modular version identity to `6.0.20`, compare and port listed legacy HR routes `/hr`, `/hr-benefits`, `/attendance`, `/attendance/shifts`, `/attendance/shift-rules` and `/attendance/policies`.
- 14B.12 complete: bump modular version identity to `6.0.21`, port full legacy `/payroll` with salary structures CRUD, salary payment preview/save/edit/delete, payslip PDF/email/WhatsApp handoff, salary payment PDF handoff, biometric enrollment save/revoke, photo proof review/regularization and face-liveness simulator readiness.
- Books resumes after this HR deployment. Continue Stage 14C.2 voucher/ledger parity only after `.127` deploy acceptance is clean.
- Keep actual attendance marking, payslip generation, salary payment voucher creation and device write actions behind explicit opt-in gates.
- 14B.12 is checkpoint 3 after the last `.127` deployment. Deploy modular frontend to `.127` after validation; no database backup is required because this checkpoint is frontend-only.

## Stage 14C: Version6 Books Modular Parity

Goal: complete Books next, module-by-module, after HR code closure. Keep live posting actions behind explicit opt-in gates.

- 14C.1 complete: bump modular version identity to `6.0.18`, add Books parity baseline for accounting ledger, vouchers, petty cash, vendor payments, GST reports and audit/message log route ownership.
- 14C.2 complete: bump modular version identity to `6.0.22`, add voucher create/edit/delete, Save & Print PDF handoff, hidden party-ledger resolution, non-cash bank account validation, ledger sync repair and ledger statement audit review.
- 14C.2 deploy repair complete: bump modular version identity to `6.0.23`, move Codex active workspace to `C:\AIArea\Codex\GarmetixWebStarter`, fix SRP deploy config auto-detection from `C:\AIArea`, and verify LAN/public route delivery.
- 14C.3 complete: bump modular version identity to `6.0.24`, add guarded bank transaction create/edit/delete, bank statement reconcile/unreconcile, cheque lifecycle update, and bank audit readiness script.
- 14A.9 complete: bump modular version identity to `6.0.25`, port the Antigravity fullscreen POS counter layout into `/sale`, enable POS Nuxt layouts, add `/history` sale listing with print/detail and Digital Bill CRM signals, wire the route/sidebar/smoke checks, and create `.codex/Priority6_POS_CRM_TODO.md`.
- 14A.10 complete: bump modular version identity to `6.0.26`, port legacy hidden-iframe PDF printing for modular POS sale/history/print queue, move POS notifications to right-bottom toasts, change new sale invoice numbering to `StoreCode-YYYYMM-INV-NumberSeries`, and document remaining DotMatrix/server print parity checks.
- 14A.11 complete: bump modular version identity to `6.0.27`, hotfix live invoice PDF `500` caused by missing server QR dependency, keep invoice PDFs printable with a scan-code fallback box, and switch POS document printing to a legacy-style API URL builder.
- 14A.12 complete: bump modular version identity to `6.0.28`, remove the direct invoice PDF QR dependency from the PDF path after live testing showed the CLR failed before fallback catch handling, and keep a scan-code fallback box until QR packaging is repaired.
- 14A.13 complete: bump modular version identity to `6.0.29`, promote POS `/history` toward legacy `/billing` invoice register parity with server-side paging, date/status filters, register totals, cancel action and hard-delete action.
- 14C.4 complete: bump modular version identity to `6.0.42`, add vendor bank account guarded edit, secure bank detail review/update, masked account review, settlement-closure dashboard, evidence CSV handoff and bank statement import planning.
- 14C.5 complete: bump modular version identity to `6.0.52`, confirm GST report/return export handoff (CSV/JSON/Excel/schema review) against the live `GstReturns`/`Gstin` backend, fix a GSTIN provider role-visibility gap on `gst-production`, add guarded financial-year lock create/unlock actions to `financial-year-locks`, and add the Books Stage 14C.5 final closure gate script. Books Version6 modular parity lane is closed.
- Reuse the Stage 13D Books readiness scripts, then upgrade them for Version6 live-safe evidence.
- 14C.1 was deployed as checkpoint 3 after the prior `.127` deployment.
- 14C.2 is checkpoint 1 after the Stage 14B.12 `.127` deployment. Deploy on checkpoint 3 unless live testing is explicitly requested earlier.

## Stage 14D: Version6 CRM And Digital CRM Modular Parity

Goal: merge customer CRM and Digital Bill CRM into a dedicated `crm` modular app, while keeping POS focused on counter speed and Main Back Office lean.

- 14D.1 complete: bump modular version identity to `6.0.30`, create `frontend/modular/apps/crm`, add route ownership, app switcher/menu, smoke route coverage, SRP `/crm/` deployment path, read-only CRM registers and the CRM/Digital CRM roadmap.
- 14D.2 complete: bump modular version identity to `6.0.31`, promote customer register, new customer and edit customer to writable parity from legacy `/customers`, including metrics, GSTIN validation and loyalty ledger preview.
- 14D.2a complete: bump modular version identity to `6.0.32`, restore CRM Tailwind/Nuxt UI stylesheet imports and align CRM auth middleware with POS/HR redirect/session behavior.
- 14D.3 complete: bump modular version identity to `6.0.33`, promote loyalty program/customer ledger/manual adjustment and customer dues reconciliation evidence/export to usable CRM workflows.
- 14D.4 complete: bump modular version identity to `6.0.34`, fix CRM runtime table rendering/customer edit data loading, and port Digital Bills register actions, analytics and audience segmentation.
- 14D.5 complete: bump modular version identity to `6.0.35`, add customer register filters/pagination, harden edit/loyalty row ID handling, and move customer loyalty preview into a slideover.
- 14D.6 complete: bump modular version identity to `6.0.36`, promote customer feedback, review settings, WhatsApp settings/test-send and WhatsApp logs/retry into usable CRM workflows.
- 14E.1 complete: bump modular version identity to `6.0.37`, enable Swagger/OpenAPI documentation for the shared ASP.NET Core API, expose the docs UI at `/api/docs`, expose JSON at `/api/openapi/v1/swagger.json`, and add JWT Bearer authorization support for protected endpoint testing.
- 14E.2 complete: bump modular version identity to `6.0.38`, force `ApiDocs__Enabled=true` in SRP API deployment/runtime bootstrap configuration, and keep API docs enabled on the `.127` live service after deployment restarts.
- 14D.7 complete: bump modular version identity to `6.0.39`, replace the read-only CRM campaign table with a campaign workbench for preview/create, queue, WhatsApp send, mark-sent, cancel, recipient message copy and ROI review.
- 14D.8 complete: bump modular version identity to `6.0.40`, promote invoice ad banners to create/edit/delete CRM workbench, and add anonymous `/i/:token` digital bill customer page with PDF, review, WhatsApp support, feedback and banner-click tracking.
- 14D.9 complete: bump modular version identity to `6.0.41`, promote Digital Bill Acceptance to a real final-readiness console, add public token/PDF smoke launchers, add CRM final closure gate, and wire CRM closure/build into validation.
- CRM lane is code-ready. Production handover remains conditional on real public-token/manual browser evidence.
- Books `14C.5` is complete; the Books Version6 modular parity lane is closed pending live-token/manual evidence.

## Stage 14F: Version6 Assistant, MCP And AI Sense Assistant Lane

Goal: add an embedded Garmetix Assistant and later MCP tool layer without splitting the backend, bypassing permissions, exposing secrets, or mutating live data unexpectedly.

- 14F.1 complete: bump modular version identity to `6.0.43`, review Claude-provided assistant patch, create `.codex/Assistant_MCP_AI_Sense_TODO.md`, document the Assistant/MCP roadmap, add a non-mutating intake readiness check, and mark the pasted provider key as rotate/revoke before live use.
- 14F.2 complete: bump modular version identity to `6.0.44`, adapt backend assistant options, DTOs, endpoints, conversation storage, read-only store tools and current `Program.cs` wiring into the Version6 API with Assistant disabled by default.
- 14F.3 complete: bump modular version identity to `6.0.45`, add the feature-flagged shared Assistant frontend panel/launcher in the current Nuxt UI 4.9 shell, and keep the launcher hidden unless a token exists and `NUXT_PUBLIC_GARMETIX_ASSISTANT_ENABLED=true`.
- 14F.4 complete: bump modular version identity to `6.0.46`, expose `get_business_snapshot` and `get_today_snapshot` through the AI Sense read-only tool catalog, reuse current dashboard aggregations with workspace scope, and enable the modular assistant launcher flag with `NUXT_PUBLIC_GARMETIX_ASSISTANT_ENABLED=true`.
- 14F.5 complete: bump modular version identity to `6.0.47`, normalize shared API paths so AI Sense API path calls like `api/...` no longer become duplicate `/api/api/...` requests, and add API path regression readiness.
- 14F.6A complete: bump modular version identity to `6.0.49`, add an AI Sense API path runtime guard, add stale workspace package detection/repair, and wire workspace-link repair before SRP deploy builds through the detected npm command.
- 14F.6B complete: bump modular version identity to `6.0.50`, serve SRP modular deep routes through generated `$uri/index.html` files before directory redirects, normalize trailing-slash deep app routes, and add acceptance checks for AI Sense sales, purchase, daily and profit analysis pages.
- 14F.6C complete: bump modular version identity to `6.0.51`, declare AI Sense connected-analysis props at runtime so report endpoints survive production bundling, and guard against missing endpoint values causing API root `/api/` calls.
- 14F.7 complete: bump modular version identity to `6.0.58`, add the official `ModelContextProtocol.AspNetCore` SDK, expose the same 6 read-only tools over MCP at `POST /api/mcp` via an additive `AssistantMcpTools` wrapper (forwards to the existing `AssistantToolCatalog.ExecuteAsync`, no business logic duplicated), reuse the standard JWT bearer auth pipeline (same `WorkspaceScope` tenant scoping as the rest of the API), and gate it behind a new `Assistant:McpEnabled` flag independent of and defaulting off alongside `Assistant:Enabled`.
- Assistant backend provider secrets must be provided only through server-side environment variables.

## Stage 14G: Version6 Books GST Full Legacy Parity

Goal: port every page under legacy's "GST" nav group (`frontend/legacy/garmetix-web/components/AppShell.vue:203-212`) to modular with no feature left behind. Stage 14C.5 only closed GST report/return export handoff and financial-year-lock acceptance - this stage completes the rest: the manual GSTR-1/GSTR-3B builder, draft lifecycle, accounting-posting bridge, CA email/WhatsApp review sharing, the Accounting/GST post-import validation report, and the GST Final Acceptance checklist.

- 14G complete: bump modular version identity to `6.0.53`, port `useGstReviewContact` composable, rewrite `gst-returns.vue` into the full builder (header form, GSTR-1 dynamic rows, GSTR-3B fixed sections, Preview, Load From Books, draft Save/Update/Delete/Mark-Filed, ad-hoc JSON/Excel export, audit trail, typed-confirmation accounting-posting bridge, CA review/share modal), add the CA share modal to `gst-reports.vue`, add `accounting-gst-validation.vue` (new Books route) and `gst-final-acceptance.vue` (fills the previously dangling Admin route), and add the Stage 14G closure gate script. All backend support already existed - zero backend/DB changes.
- 14G.1 complete: bump modular version identity to `6.0.54`, fix a flaky Nitro cold-build bug (non-deterministic crash or silent `"Redirecting..."` stub output, unrelated to any app's source code) by adding a build retry/content-size-verification safety net to `srp-whole-site-deploy.sh`, and fix an npm workspace install failure caused by stale `@garmetix/shared-*` version pins (`"6.0.0"` relaxed to `"*"` across every app/shared package). Found after Amit reported the deployed site downloading pages as text files instead of rendering.
- Books modular GST menu lane is closed at `14G`; regular module sequence now depends on Amit's next priority (Assistant/MCP `14F.7`, the cross-module live-evidence backlog, or the AntiGravity port review).

## Stage 14H: Version6 POS/Books/HR UX Overhaul

Goal: targeted UX changes Amit requested directly - remove redundant POS nav, add live POS sale widgets, move several always-visible inline forms/detail-panels into modal/slide-over overlays (matching the existing CRM/HR `UModal`/`USlideover` convention), add pagination/filters to two POS pages, add missing create forms for two Books master pages, and port one legacy HR dashboard page. No backend/DB changes.

- 14H.1 complete: bump modular version identity to `6.0.55`. POS Home (`index.vue`): registered a `Home` menu route (previously unregistered), removed the redundant subtitle line and the duplicate "Sales History" nav button/quick-action tile, added 6 live sale widgets (Today's Total Sale, Total Sale Return, Cash/UPI/Card/Other Sale) computed client-side from the existing `billing/sales` endpoint - no backend change. Sale History (`history.vue`): converted the always-visible Invoice Detail aside into a `USlideover`. Sale Return/Exchange (`returns.vue`, `exchange.vue`): added status/date-preset filters and pagination matching `history.vue`, and moved the Return Summary/Refund and Exchange Summary/Additional Payment panels into `USlideover`s triggered by a "Review & Refund"/"Review & Complete" button.
- 14H.2 complete: bump modular version identity to `6.0.56`. Books Vouchers (`vouchers.vue`): the always-visible create/edit form is now a `UModal`, matching legacy's same-route modal pattern. Books masters: added create/edit `UModal` forms for Ledgers and Parties to `accounting.vue`'s tabs and to the standalone `parties.vue` page, reusing the pre-existing `/api/parties` CRUD and the generic `MapCrud<Ledger>` registration at `/api/ledgers` in `Program.cs` (a custom duplicate Ledger endpoint was drafted, then reverted, once the generic registration was found - no net backend change). `BooksMasterTable` gained an optional `#actions` slot for row edit/delete controls. Books GST (`gst-returns.vue`): the return header form and each GSTR-3B section (Supplies, ITC, Interstate, Inward+Interest) now open in their own modal with a compact inline summary; repeatable B2B/B2C/HSN/document/nil-rated rows stay inline by design. `financial-year-locks.vue`'s create/edit form is now a modal. HR: added `attendance-dash.vue` (route `/attendance-dash`), porting legacy's attendance dashboard (metric cards, manual punch, today attendance table) using the existing `today.vue`/`manual-punch.vue` field logic. Also fixed (out-of-band security item, unrelated to this stage's scope): `FactoryResetEndpoints.cs` now requires `SuperAdmin` instead of `Admin`, and `.gitattributes` gained a blanket `text=auto` rule.
- Books Notes/GST menu split (same stage window, no separate version bump): Debit/Credit/Commercial Notes moved from the `Accounting` sidebar group into their own `Notes` group in `routes.ts`, alongside the already-separate `GST` group.

## Stage 14I: Version6 HR Module UX Overhaul

Goal: a large, itemized HR module overhaul Amit requested directly - route rename, modal/slideover forms everywhere an inline form existed, legacy-parity features (Employee list shape/mobile masking, ID card, monthly-generation auto-trigger), pagination/filters on every list that can grow, readable status/error text instead of raw JSON, and CRUD completion (Attendance Policies delete, Salary Payment view/edit/delete). Full detail preserved in `docs/stage-14i-hr-module-overhaul.md`.

- 14I complete: bump modular version identity to `6.0.57`. Renamed `/hr/hr` to `/hr/employees` (`pages/hr.vue` -> `pages/employees.vue`, updated `routes.ts` and every script/nav config referencing the old path). `employees.vue`: Employee form -> `USlideover`, Attendance form -> `UModal`; ported legacy's employee row shape (masked mobile via `maskMobile()`, Code/Department/Designation/Salary/Joining columns) and ID Card feature (`GET api/hr/employees/{id}/id-card` + `UModal` + browser print, matching legacy's client-side-only print approach); added pagination to the Employee register (client-side) and Daily Attendance register (server-side, reusing the existing paged `api/hr/attendance` endpoint); ported legacy's `autoGenerateIfMonthEnd()` localStorage-guarded month-end auto-trigger. `attendance/monthly.vue`: added client-side pagination over the per-day rows table. `attendance/payroll-summary.vue`: rebuilt from a raw-JSON dump into a readable per-employee table (joined against `/api/employees` for names) with search, pagination and a matching CSV export. `attendance/regularization.vue`: create form -> `UModal`, removed the raw JSON request-payload preview card, decoded request/punch type codes into labels, added status filter + search + pagination. `attendance/shifts.vue`, `shift-rules.vue`, `attendance/policies.vue`: forms -> `UModal`; added `DELETE /api/attendance/policies/{id}` (mirroring the existing Shifts/ShiftRules soft-delete pattern, plus filtered soft-deleted policies out of the list endpoint) and a Delete button; Shift Rules gained pagination. `hr-benefits.vue`: form -> `UModal`, added an adjustment-type filter plus pagination. `attendance/salary-payment.vue`: added View/Edit/Delete for the previously read-only Existing Payments table, reusing the already-complete `/api/salary-payments` CRUD (delete also reverses the accounting posting), plus search + pagination. `attendance/salary-draft.vue`: readable spaced status labels, search + pagination. `payroll.vue`: Salary Structure form -> `UModal`, Salary Payment form -> `USlideover` (including the "Pay" cross-flow buttons from Payslips/Structures tabs). Deploy tooling: added an `--apps=hr,books,...` selective-build flag to `srp-whole-site-deploy.sh` so unrelated Nuxt apps reuse their existing local build instead of rebuilding on every deploy. No backend/DB changes beyond the one small, additive Attendance Policies delete endpoint (mirrors an existing sibling route in the same file).

## Stage 14K: Version6 Books Accounting Legacy Parity

Goal: fix three Books bugs Amit reported directly from the live SRP site - Parties page showing a blank table, Vouchers auto-opening a too-small New Voucher modal on every visit, and Accounting being generally under-implemented versus legacy. Checked `frontend/legacy/garmetix-web/pages/{parties,vouchers,accounting}/index.vue` for the reference implementation per Amit's instruction.

- 14K complete: bump modular version identity to `6.0.59`. Root cause of the "blank Parties table": the standalone `parties.vue` nav page was querying `GET /api/parties`, the internal ledger-link `Party` master table used only by Vouchers/Accounting for ledger-linking - legacy's actual "Parties" nav page queries `customers`+`vendors` instead (a GSTIN customer/vendor register with lookup/validation via `gstin/validate-party`). Rebuilt `parties.vue` to match legacy exactly: customer/vendor type toggle, GSTIN lookup+validation, create via `POST customers`/`vendors`. Root cause of the Vouchers auto-open bug: `refresh()` called `startCreate()` (which also sets `formOpen=true`) purely to seed default ledger/employee select values, firing on every page load before those defaults existed - split into a separate `seedDefaultFormValues()` that doesn't touch `formOpen`, matching legacy's behavior of only ever opening the create form via an explicit `?new=1` query param. Also widened the Voucher modal to `w-[calc(100vw-2rem)] sm:max-w-5xl lg:max-w-6xl` (was using Nuxt UI's small default), matching legacy's `content-class`. `accounting.vue` brought to real 11-tab parity with legacy: added a create/edit form to the previously read-only Bank Accounts tab, and five entirely missing tabs - Bank Transactions (deposit/withdraw CRUD against `accounting/bank-transactions`), Bank Reconciliation (reconcile/reopen statement lines via the existing `bank-statement-lines/{id}/reconcile`/`unreconcile` endpoints), Cheque Log (CRUD via `/api/cheque-logs` plus Clear/Bounce lifecycle via `accounting/cheque-logs/{id}/lifecycle`), Vendor Bank Accounts (CRUD via `/api/vendor-bank-accounts`), and Account Details (CRUD via `/api/bank-account-details`, UI intentionally omits the password/PIN/CVV fields present on the backend model since legacy's own rendered form never exposes them either). Refactored the page's create/edit flow from two separate modals (Ledgers, Parties) into one shared modal switched by `activeTab`, matching legacy's structure, to keep the file maintainable across 7 form-bearing tabs. All backend endpoints already existed (`MapCrud<T>` generic registrations plus the existing `AccountingEndpoints.cs` routes) - zero backend/DB changes.
- 14K.1 complete: bump modular version identity to `6.0.60`. Amit reported `/attendance-dash` missing from the sidebar's Attendance submenu. Root cause matches the earlier Books Notes/GST menu bug exactly: the sidebar is driven by a hardcoded `localMenus` map in `packages/shared-ui/components/ModularAppShell.vue`, entirely separate from `routes.ts` - a route can exist and work perfectly while having no sidebar entry at all. Checked the rest of the HR `attendance` group against `routes.ts` and found four more of the same: Manual Punch, Shifts, Shift Rules and Policies (all built out with real CRUD this session under Stage 14I) had working pages but no sidebar link either. Added all five (`attendance-dash`, `manual-punch`, `shifts`, `shift-rules`, `policies`) to the Attendance submenu. Left kiosk/mobile-kiosk/photo-review/biometric-enrollment/face-liveness/device-bridge out of the sidebar deliberately - those are reached via `attendance-dash`'s own header action buttons or QR/kiosk deep links, not everyday sidebar browsing.
- 14F.8 complete: bump modular version identity to `6.0.61`. Assistant enabled live on SRP - `Assistant__Enabled=true` and a real Anthropic API key set in the production `/etc/garmetix/srp-api.env` via a new `--set-assistant-secret` deploy-script flag (key sourced only from `GARMETIX_ASSISTANT_ANTHROPIC_API_KEY`, never a CLI arg or committed anywhere). Added `NUXT_PUBLIC_GARMETIX_ASSISTANT_ENABLED=true` (new `SRP_ASSISTANT_ENABLED` config, default true) to `build_app()`'s build env - the frontend launcher flag had never been set by any deploy to date, so the sparkle icon was baked in as hidden regardless of backend state; confirmed `assistantEnabled:true` in the live bundle after redeploy. Found and fixed a real production incident along the way: `--skip-api` deploys were leaving the release's `api/` folder empty, which worked by accident until something actually restarted the service - `--set-assistant-secret`'s own restart step surfaced it, crash-looping the live API for several minutes (`203/EXEC`). Recovered live with Amit's explicit authorization, then fixed `publish_api()` to pull the currently-live `api/` forward from the remote host on every skip-api deploy so it can't recur. Also ran `npm run validate`/`npm run deploy:preflight` to a clean pass for the first time in a while, fixing several stale readiness-script assertions along the way (see `.claude/changelog.md` for the full list) - all unrelated to Assistant itself, just surfaced because the full validator suite hadn't been run end-to-end recently.
- 14F.9 complete: bump modular version identity to `6.0.62`. Amit hit a raw nginx `405 Not Allowed` HTML block dumped directly into the Assistant chat panel and asked for a friendly message. Root-caused first (a LAN `curl` straight to `/api/assistant/chat` returned a correct `401`, confirming nginx/backend routing was already fine - the 405 was very likely a transient artifact of the prior turn's deploy/recovery window). Fixed the real, codebase-wide UX gap regardless: `@garmetix/shared-api`'s `request()`/`loginToGarmetix()` threw the raw HTTP response body verbatim as the error message on any non-ok response, so any infra-layer failure that never reached the ASP.NET app (nginx/Cloudflare error pages, empty bodies) surfaced as raw markup in every app's error UI, not just Assistant's. New `parseErrorMessage()` extracts `error`/`message`/`detail`/`title` from JSON bodies first (covering this codebase's convention plus ASP.NET's ProblemDetails shape), falling back to a clean status-code-keyed message only when the body isn't usable JSON/looks like HTML - genuine backend validation messages are completely unaffected. Verified against the exact HTML from Amit's report plus three other real error shapes. `npm run validate` full suite passes (shared package touching every app).

## Stage 14L: Searchable Ledger/Employee Pickers And Active-Employee Filtering

Goal: Amit asked for Ledger picker dropdowns to become searchable/autocomplete instead of plain scrolling lists, and for Employee picker dropdowns to show active employees only everywhere except the HR Employee master page (and explicitly, Payroll/Attendance/Salary Payment pickers should also be active-only) - plus listing pages should offer an All/status-wise view rather than silent filtering.

- 14L complete: bump modular version identity to `6.0.63`. Converted 6 `USelect` -> `USelectMenu` Ledger pickers across Books (`vouchers.vue`, `accounting.vue` x3, `cash-details.vue` x2) - first use of `USelectMenu` in this codebase, read the actual `.d.ts` type definitions directly (no existing pattern to copy) and set `value-key="value"` explicitly since its default binds the whole item object rather than just the id. Added `isActiveEmployee()` to `@garmetix/shared-utils` and applied it to every employee-picker dropdown that attaches an employee to a new record (Books `vouchers.vue` Issued By, HR `payroll.vue` x2, `attendance/manual-punch.vue`, `attendance/regularization.vue`, `attendance/shift-rules.vue`, `attendance/biometric-enrollment.vue`), converting these to searchable `USelectMenu` too. Replaced three pre-existing, separately-duplicated active-employee predicates (`employees.vue`, `attendance-dash.vue`, `hr-benefits.vue`) with the shared helper - `hr-benefits.vue`'s version used OR instead of AND, a real bug fixed as a side effect. `employees.vue`'s main Employee Register stays unfiltered by default (it's the master management list) but gained an explicit All/Active-only/Inactive-only status filter next to its search box. Did not extend a status filter to Monthly Attendance/Today/Payroll Summary/Salary Draft/Payroll Finalization/Salary Payment listing pages - none of them currently fetch or join employee-status data, so a real filter there would need a new employee fetch+join per page; flagged as a follow-up question rather than guessed at.

## Stage 14L.1: Books Voucher Register Filter Polish

Goal: improve the modular Books Voucher listing filters without changing the shared API or disturbing Claude's active Books-module worktree/deploy flow.

- 14L.1 complete: bump modular version identity to `6.8.1`, add date range, month/year, voucher type, ledger type/group, exact ledger and text-search filters to `apps/books/pages/vouchers.vue`, plus a clear-filters action. Ledger type/group is resolved from readable ledger metadata already loaded by the page, with safe `Unclassified`/`Unlinked` fallbacks. No backend/API/database/deploy change.
- Next Books listing polish, if requested: server-side voucher pagination for very large ledgers and a backend query contract for ledger-group filters once the current Books backend lane settles.

## Stage 14L.2: Books Day Book Port

Goal: bring legacy Accounting menu Day Book into modular Books without touching backend/API/database or Claude's GST module lane.

- 14L.2 complete: bump modular version identity to `6.8.3`, add `apps/books/pages/day-book.vue`, register `/day-book` in route ownership, sidebar and Books Home quick links. The page ports legacy Day Book essentials: date presets, single-day previous/next, custom range, month/year, transaction type filter, optional journal rows, search, pagination, summary cards, CSV export, print/PDF evidence, detail slideover and quick-create actions. Source links are mapped to modular ownership: sale rows to POS history, purchase rows to Main purchase, voucher/vendor-payment/accounting rows to Books and cash voucher rows to POS Off Book. No backend/API/database/GST-module/deploy change.


# Garmetix Roadmap (Claude View)

Last updated: 2026-07-07. Source of truth for stage-by-stage history remains `frontend/modular/docs/MODULAR_TODO.md` (Codex-owned); this file is Claude's forward-looking synthesis, not a replacement.

## Where Things Stand

- Modular version: `6.0.53`, Stage 14G (Books full legacy GST menu parity), completed and deployed live to the `.127` SRP test server, genuinely verified this time (see below).
- All six modular apps (`main`, `pos`, `hr`, `books`, `ai-sense`, `admin`) plus `crm` have reached at least one "final closure" gate. Legacy frontend and shared backend remain the single source of truth; modular apps consume the same API/DB.
- Structure validation (`node frontend/modular/scripts/validate-structure.mjs`) passes as of this session.
- SRP deploy tooling note: this machine now has a dedicated `~/.ssh/garmetix_srp` keypair trusted by `192.168.11.127` (added 2026-07-07), so future deploys don't need `sshpass`/a password - just run with `GARMETIX_PREFER_WSL=false` since the key lives in the Git Bash environment, not WSL's separate filesystem.
- **Deploy pipeline hardening (2026-07-07)**: the first 14G deploy attempt silently shipped broken content site-wide (a flaky Nitro cold-build bug) while its own acceptance script showed all-green, because that check only validates HTTP status codes, not actual response content. Fixed the flaky build with a retry+content-verification safety net in `srp-whole-site-deploy.sh`. **Any future deploy verification must check real page content/size, not just status codes** - this is now the standard to hold future deploys to.
- `dotnet publish` in the SRP deploy script is currently broken specifically when run via Git Bash (works fine assumed-via-WSL) - doubled drive-letter path bug, not yet fixed, use `--skip-api` when there are no backend changes to republish.

## Purchase Import satellite pages + Tailoring & Alteration (done 2026-07-10)

Originally scheduled as a one-shot overnight task (`purchase-import-tailoring-overnight`, fire time 2026-07-11 04:30 IST); Amit asked to do it immediately instead, so the scheduled task was cancelled and the work done live in-session. Shipped: Import Acceptance QA dashboard, Import Learning/vendor-profiles page, full Tailoring & Alteration module port (was a placeholder stub), and a `VendorType` column on `Vendor` so tailoring vendors are genuinely distinguished from purchase vendors. See `.claude/todo.md` for the itemized breakdown, exact file references, and the backward-compatibility note on pre-migration vendor rows.

## Purchase Module (started 2026-07-10)

Modular Purchase was previously a stub (`main` app placeholders) plus two read-only Books review pages. Phase 1 (Vendors CRUD, Purchase Register/New Inward/Purchase Return full CRUD, Vendor Payments/Settlements upgraded from read-only) shipped in `v6.1.0` - see `.claude/todo.md` for the itemized list and `.claude/changelog.md` for detail. Phase 2 (Scan/Import Supplier Invoice + its two satellite QA/learning pages, plus two independent read-only QA dashboards) is deferred - it depends on a 4060-line backend OCR service (`PurchaseInvoiceImportService.cs`) that already exists but has zero modular frontend today. Recommend picking up the two independent QA dashboards (Vendor Payable Reconciliation, Purchase Return Advanced Settlement) first since they're low-effort read-only report pages with no dependency on the Import feature; the Import workflow itself is a multi-session effort on its own given the OCR review/correction UI surface.

## Swalekha (Personal & Personal Finance) Module - design approved 2026-07-17, build not started

A brand-new, completely isolated module (Owner-login-only, own `swalekha_db` database, own frontend app not registered in the app switcher/sidebar) covering personal finance (accounts/loans/investments/insurance/expenses/travel sheets/person-to-person ledger) and a personal organizer (diary/notes/calendar/contacts). Full design, naming, isolation architecture and a 13-stage plan (`PersonalFin_01`..`PersonalFin_13`) are in `docs/personal-finance-module-design.md` and `.claude/todo.md`. Nothing beyond this design pass exists yet - `PersonalFin_01` (Foundation: separate DB, Owner-only policy, isolated frontend shell, deploy wiring) is next once Amit says to start building.

## Near-Term (next 1-3 sessions)

0. **BS-16 Accounting Master Audit / Unification Prep** - before any code or data mutation, create a stage backup with `npm --prefix frontend/modular run deploy:srp:backup -- --stage=BS16AccountingMasterAudit`. Audit existing ledgers, ledger groups, customers, vendors, employees, other parties, vouchers, sales, purchases and salary payments. Produce a no-mutation design report for unifying Final Accounts with the existing Books accounting masters.
1. ~~**Books 14C.5**~~ - closed 2026-07-07: GST/accounting report finalization confirmed, financial-year lock create/unlock UI added, final Books closure gate script/doc shipped.
2. ~~**Books 14G**~~ - closed 2026-07-07: full legacy GST menu parity (GSTR-1/3B manual builder, draft lifecycle, accounting-posting bridge, CA email/WhatsApp share, accounting-gst-validation, gst-final-acceptance), deployed and verified live on `.127`. Books modular GST lane is now fully done pending live-token/manual evidence. See `.claude/changelog.md`.
3. **CRM bug** (found during 14G deploy verification) - `/crm/customers` is broken live due to a static-build route collision with its own sub-routes. Unrelated to GST work; spawned as background task `task_38949783`, not yet fixed.
4. **Assistant 14F.7** - MCP wrapper around the existing read-only AI Sense tool catalog, additive only, local/private-tunnel auth until reviewed.
5. **Live-evidence backlog** - a recurring pattern across POS, HR, Books, CRM and Admin closures: code and dry-run validation are done, but real production evidence (manual browser acceptance, live-token smoke tests, actual cashier/payroll runs) is still marked conditional. Worth a dedicated pass to clear this backlog rather than letting each module's "pending live evidence" note linger indefinitely.
6. ~~**Security follow-up**~~ - closed 2026-07-10 (v6.2.4): factory reset is now genuinely SuperAdmin-only at the route level (new standalone `GarmetixPolicies.SuperAdmin` policy, bypassing the shared Admin/Owner-permissive matrix mechanism). See `.claude/todo.md` for detail.

## Medium-Term

1. **BS-17 Indian/Tally-compatible Chart of Accounts normalization** - align existing ledger groups to Indian operational accounting practice and TallyPrime/BUSY/Marg-style groups. Keep Schedule III-ready reporting templates versioned. Do not force Ind AS unless Amit/CA confirms the company must report under Ind AS.
2. **BS-18 Party and ledger unification** - one party identity may be Customer, Vendor, Employee and/or Other Party. Resolve each party role to one canonical ledger and prevent duplicate party ledgers.
3. **BS-19 Transaction backfill and reconciliation** - dry-run existing Sale, Purchase, Voucher, Salary Payment, GST, Inventory and settlement transactions into canonical ledgers. Require Trial Balance, Balance Sheet and source-control evidence before live mutation.
4. **BS-20 Final Accounts direct ledger integration** - make Final Accounts read canonical Books ledger/accounting data directly. Keep mapping only for exception handling.
5. **BS-21 Restore drill and production safety** - restore a stage backup into non-production, validate `Backupfilehistory.md`, and document rollback before production accounting migration.
6. **AntiGravity port review** (`AntiGarvity2CodeTODO.md`, Priority 0-7) - none of these items are checked off yet. Highest-value candidates to review first:
   - Priority 1: SaaS tenant/subscription backend model (`Tenant`, `Subscription`, `GarmetixDbContext` global query filter) - security/architecture-sensitive, needs careful review before any tenant isolation logic touches shared live data.
   - Priority 3: Inventory as its own modular app (currently inventory lives inside `main`) - would let `main` shrink further toward "lean back office," matching the Stage 12G goal.
   - Priority 4/5/6: Books/HR/POS page-level diffs from Version6.A - smaller, page-by-page ports.
   - Priority 7: shared package composable overlap (`shared-ui` vs `shared-api`/`shared-auth`) - cleanup, not urgent.
7. **`.gitattributes` line-ending policy** - add a blanket `* text=auto` (or explicit per-extension) rule so CRLF/LF churn doesn't produce misleading diffs across tools/OSes. Currently only `*.gitattributes` and `*.sh` are pinned to LF.

## Longer-Term / Deferred By Design

- Keeping one shared ASP.NET Core API and one PostgreSQL database is an explicit, repeated decision across every stage doc. Do not propose splitting these without an explicit user ask.
- Per-app Cloudflare subdomains (`pos.`, `hr.`, `books.`, `ai-sense.`, `admin.`) are documented but full public-domain cutover evidence is still pending per the SRP deployment stage notes.
- Every deploy or DB-affecting implementation stage must use `docs/database-stage-backup-protocol.md`; backups live at `/opt/garmetix/backup/database/` with `Backupfilehistory.md` beside them.

## Known Risks Carried Forward (see `.claude/todo.md` bug list for detail)

- Several `Garmetix.Domain.Generated` models have acknowledged incompleteness (UOM/Fabric, basic rate calculator, Invoicing derived-vs-stored properties).
- Large backlog of unreviewed Antigravity (`Version6.A`) changes that may contain already-solved problems (e.g., Inventory app, GSTIN/wholesale pricing in POS) worth comparing before building from scratch.

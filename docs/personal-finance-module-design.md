# Swalekha — Personal & Personal Finance Module (Design)

Status: **Design approved 2026-07-17, build not yet started.** This document is the reference design for the `PersonalFin_01` .. `PersonalFin_13` build stages tracked in `.claude/todo.md`. Nothing described here exists in code yet.

## 1. Why This Module Exists

Amit asked for a brand-new module for his own personal life and finance management, completely separate from the Garmetix business platform (Aadwika Fashion's retail/accounting suite). It must be usable by **only the Owner login** and must not appear anywhere in the shared app-switcher or sidebar that links the existing business apps (main/pos/hr/books/crm/admin/inventory/ai-sense/final-accounts) together.

It covers two pillars:
- **Personal Finance** — bank accounts, loans, investments (FD/RD/Mutual Funds/SIP/Shares), insurance, person-to-person lending, expense/income tracking including trip-scoped travel expenses and a private "hidden expenses" category.
- **Personal Organizer** — diary/journal, personal notes, calendar & appointments, contacts.

## 2. Decisions Confirmed With Amit

| Decision | Choice |
|---|---|
| App/module name | **Swalekha** (स्व-लेखा — "self" + "ledger/writing"; the word carries both the finance/ledger sense and the diary/writing sense, fitting both pillars) |
| URL | Same domain, new path: `srp.aadwikafashion.in/swalekha` |
| Access | **Owner userType only, strictly.** SuperAdmin and Admin are refused — no break-glass exception. |
| Login | Same Garmetix JWT/login system, gated by a new Owner-only backend policy |
| Database | **Separate PostgreSQL database** (`swalekha_db`), run by the same existing ASP.NET Core API process — not a separate backend service |
| Discoverability | Not listed in the app switcher or any sidebar. Reachable by direct URL, plus one small Owner-only link in the existing apps' profile/account dropdown menu |

## 3. Isolation Architecture

### Frontend
New Nuxt workspace at `frontend/modular/apps/swalekha/`, structured like every other modular app (`pages/`, `components/`, `nuxt.config.ts`, `package.json`) but with its **own minimal top bar** — deliberately not reusing `ModularAppShell.vue`'s cross-app switcher component, so there is no code path in Swalekha that could ever surface a link to or from the business apps. It may still consume `@garmetix/shared-ui` primitives (buttons, tables, modals, form inputs) for build speed and visual consistency — just not the shell/switcher piece. Dev port `3109` (next free port after the existing apps' `3100`–`3108`).

### Backend
Same ASP.NET Core API process and deploy unit as today. A new `SwalekhaDbContext` is registered alongside the existing `GarmetixDbContext`, pointed at its own connection string (`ConnectionStrings:SwalekhaDb`). A new `GarmetixPolicies.SwalekhaOwner` policy is registered **directly in `Program.cs`'s `AddAuthorization` block**, bypassing the shared `AddMatrixPolicy`/`AccessPermissionMatrix.CanAccessPolicy` mechanism entirely (that mechanism grants a blanket pass to SuperAdmin/Admin/Owner alike for every matrix-registered policy, which cannot express "Owner only"). This mirrors the exact precedent already set for the SuperAdmin-only factory-reset fix: the new policy asserts `AccessPermissionMatrix.IsOwner(user)` directly (a public helper that already exists, currently only used inside `IsAdminOrOwner()`), with no SuperAdmin or Admin fallback. Every Swalekha endpoint lives under `/api/swalekha/*` and requires this policy.

### Database
`swalekha_db` — a genuinely separate Postgres database, not new tables inside the existing shared database. New Domain models live under `backend/Garmetix.Domain/Generated/Models/Swalekha/`, using plain `BaseEntity` (no `CompanyBase`/`StoreBase` scoping — this data belongs to one person, not the multi-tenant Company/StoreGroup/Store hierarchy the rest of the platform uses). Since the SRP production host runs `Database:SchemaBootstrapMode=FreshBaseline` (individual EF migration files are never applied there — `EnsureCreatedAsync()` handles first creation, and `DatabaseSchemaRepairService.cs`'s idempotent `CREATE TABLE IF NOT EXISTS`/`ALTER TABLE ... ADD COLUMN IF NOT EXISTS` calls are what keep production schema correct after that), Swalekha needs its own equivalent idempotent repair method (`RepairSwalekhaStorageAsync`) run against the second connection on every API startup, in addition to a normal hand-written EF migration for local/dev use.

### Deploy
`frontend/modular/deploy/srp-whole-site-deploy.sh` gets a new `build_app swalekha ...` entry and a new Nginx `location /swalekha/ { ... }` block, matching every existing app. It is **deliberately excluded** from `patch_static_runtime_config` (the step that injects an app's URL into every other app's `appUrls` runtime config) and from `frontend/modular/config/apps.ts`'s `GarmetixFrontendId`/`appLabels` registry — confirmed via repo research that omission from both is sufficient to keep an app fully invisible to the app switcher and sidebar search, with no extra "hide this app" flag needed anywhere.

### Mandatory backup protocol
`CLAUDE.md`'s 2026-07-14 mandatory stage-backup protocol currently covers one production database. Once `swalekha_db` exists, it needs its own explicit line in that protocol (own dump file naming convention, own or shared `Backupfilehistory.md` entry) so every future Swalekha implementation stage that touches its schema follows the same "backup before mutation" discipline the rest of the platform already follows — this is addressed as part of `PersonalFin_01`.

### Discoverability
One small, Owner-role-conditional link is added to the shared profile/account dropdown component used across the existing business apps, pointing at `/swalekha`. This is the **only** shared-code touchpoint between Swalekha and the rest of the platform — everything else about the module (frontend shell, backend policy, database) is fully separate.

## 4. Feature Design

### Pillar A — Personal Finance

**A1. Accounts Hub** — the single registry of every financial account: Bank Accounts (savings/current, running transaction ledger, opening balance), Cash in Hand, Credit Cards (limit, statement date, due date, outstanding). Manual deposit/withdraw/transfer entries between own accounts.

**A2. Investments**
- Fixed Deposits & Recurring Deposits — principal, interest rate, tenure, bank, maturity date/amount, auto-renew flag, TDS tracking.
- Mutual Funds + SIP tracker — folio/scheme/AMC, SIP vs. lumpsum, units, purchase NAV, manually-updateable current NAV, computed returns/XIRR, SIP due-date reminders.
- Shares/Stocks — demat account, broker, buy/sell transaction history, average cost, manually-updateable current price, realized/unrealized P&L.
- Optional simple asset entries for PPF/EPF/NPS and Gold/Digital Gold (common Indian retirement/asset classes) — tracked as balance + contribution history, not full instrument-specific logic in v1.

**A3. Loans**
- Loans Taken — personal/home/car/gold/education loan, principal, interest rate, EMI, tenure, lender, EMI schedule/amortization table, prepayment tracking, live outstanding balance.
- Loans Given — to a Contact (friend/family), principal, optional interest, repayment tracking, outstanding balance — linked into the Person-to-Person Ledger (A5) rather than duplicated.

**A4. Insurance** — policies (life/health/term/vehicle/property/ULIP), insurer, policy number, sum assured, premium amount + frequency + due date, nominee, maturity date (endowment/ULIP), renewal reminders.

**A5. Person-to-Person Ledger** — a mini debit/credit ledger per Contact, for money lent or borrowed outside formal loan accounts (a friend owes you, you owe a relative, etc.): each transaction, running balance, settle/close action. This is the personal analogue of the existing Books module's Party Ledger concept, scoped entirely to Swalekha's own database and Owner-only.

**A6. Expense & Income Management**
- Expense Sheets, categorized by a Sheet Type: Personal, House/Household, Medical, Hidden, plus user-definable custom types (the schema supports adding new types without a code change).
- Income entries — salary, business drawing, rental, interest, dividend — needed so cash-flow reporting isn't expense-only.
- Recurring bills/subscriptions with due-date reminders.
- Category/tag-level budgets with actual-vs-budget tracking.

**A7. Travel Expense Sheets** — each Trip is its own scoped expense sheet (name, destination, start/end date, budget). While the trip is open, expenses are entered against that sheet; a "Close Trip" action rolls every line item up into the main Expense ledger under the Travel category, while retaining the original trip tag so travel spend can still be drilled into per-trip in reports. This matches Amit's description exactly: "each trip has its own expenses" that then "transfer to normal expenses."

**A8. Hidden Expenses** — any expense entry can be flagged Hidden. Hidden entries are excluded from the default dashboard/summary/report views and only appear when the Owner explicitly toggles "show hidden" — giving a private category for entries the Owner doesn't want surfaced in routine glance-views, without needing a whole separate app mode.

**A9. Reports & Dashboard** — Net Worth (sum of Accounts + Investments − Loans/Credit Card outstanding), cash-flow/income-vs-expense trend, an upcoming-dues feed (EMI, premium, FD/RD maturity, SIP dates — this feed also populates the Organizer Calendar in Pillar B), investment portfolio allocation by asset class, expense breakdown by sheet-type and by trip, and Excel/PDF export (useful for personal tax-time summaries of 80C/80D-relevant premiums and investments).

### Pillar B — Personal Organizer

**B1. Diary / Journal** — dated daily entries, private by default, one entry per day (or multiple, TBD at build time).

**B2. Personal Notes** — topic-based free-form notes with folders/tags, distinct from the dated Journal (Notes are reference material, Journal is a daily log).

**B3. Calendar & Appointments** — a personal event calendar; manually-added appointments plus automatic entries fed from Pillar A's upcoming-dues (EMI/premium/FD maturity/SIP), so financial deadlines show up alongside personal plans in one place.

**B4. Contacts** — a personal contact directory, separate from the CRM app's business customer records, and the source both the Person-to-Person Ledger (A5) and Calendar appointments link against.

### Pillar C — Multi-Owner & Family (added `PersonalFin_06`/`07`, 2026-07-17)

Swalekha is usable by more than one Owner login (e.g. two owners of the same company) — a gap the original design didn't account for. Every table now carries an `OwnerId`, enforced by a global query filter, so each Owner's financial data is completely private to them by default (see Isolation Architecture below).

**C1. Owner Profile** — PAN, Aadhar, Passport number, contact phone/email, address, and a linked primary bank account (one of the Owner's own `SwalekhaAccount` rows). Auto-provisioned on first use from the shared `GarmetixDbContext`'s `Employee`/`EmployeeDetail` tables when the logged-in Owner's `AppUser.EmployeeId` is set (PAN/Aadhar/Mobile/bank fields/spouse name already exist there — re-typing them in Swalekha would be redundant), editable afterward.

**C2. Family Connections** — a list of family members (spouse, children, parents, etc.) per Owner. A family member can optionally be *linked* to another real Owner login on the same platform (matched by email/username) — linking is reciprocal, so both sides see the connection. Linking does **not** expose either owner's private financial data to the other; it only enables C3.

**C3. Family Transfer Sync** — sending money to a *linked* family member needs only one entry: the sender's own withdrawal. If the recipient is a linked Owner with a primary account configured, the system automatically posts the matching deposit into *their* account on their behalf (a deliberate, narrow, server-side exception to the owner-scoping in C0 — the sender never sees the recipient's account details, only that the transfer was synced).

## 5. Staged Roadmap

Tracked as `PersonalFin_01` through `PersonalFin_15` in `.claude/todo.md`; each stage follows this project's established pattern (research → build → validate with real build/test runs → update `CLAUDE.md`/`.claude/todo.md`/`.claude/changelog.md` → version bump), same as the GST & Taxes and Final Accounts multi-stage modules.

1. **`PersonalFin_01` Foundation** — `swalekha_db` + `SwalekhaDbContext`, `GarmetixPolicies.SwalekhaOwner`, the new isolated `swalekha` frontend app (own shell, no switcher wiring), deploy wiring (path + Nginx, deliberately excluded from switcher registration), the one Owner-only profile-menu link, the backup-protocol doc update, and an empty dashboard shell. This stage exists purely to prove the isolation model end-to-end (login as Owner works, every other role is refused, the app is invisible from the switcher, the database is genuinely separate) before any real feature is built on top of it.
2. **`PersonalFin_02` Accounts Hub Core** — Bank Accounts, Cash, Credit Cards; per-account ledger; manual deposit/withdraw/transfer.
3. **`PersonalFin_03` Contacts + Person Ledger** — Contacts master; per-person dr/cr ledger (loan given/taken, repayments, settle).
4. **`PersonalFin_04` Expense & Income** — Expense sheets by type (including Hidden), Income entries, categories/tags, recurring bills + reminders.
5. **`PersonalFin_05` Travel Expense Sheets** — trip-scoped sheets; Close-Trip roll-up into the main Expense ledger under Travel, trip tag retained for reporting.
6. **`PersonalFin_06` Multi-Owner Data Isolation** — `OwnerId` on every table, global query filter, auto-stamp on insert. Foundational fix, not a feature.
7. **`PersonalFin_07` Owner Profile + Family Connections + Transaction Sync** — Pillar C above, plus Employee-table auto-provisioning.
8. **`PersonalFin_08` Investments I** — Fixed Deposits + Recurring Deposits.
9. **`PersonalFin_09` Investments II** — Mutual Funds + SIP tracker.
10. **`PersonalFin_10` Investments III** — Shares/Stocks, optional PPF/EPF/NPS/Gold.
11. **`PersonalFin_11` Loans** — Loans Taken (EMI/amortization/prepayments) + Loans Given (linked to Stage 03's Person ledger).
12. **`PersonalFin_12` Insurance** — Policies, premium reminders, nominee/sum-assured/maturity tracking.
13. **`PersonalFin_13` Personal Organizer** — Diary/Journal, Personal Notes, Calendar & Appointments (including auto-populated finance due-dates).
14. **`PersonalFin_14` Dashboard & Reports** — unified home dashboard (net worth, upcoming dues, today's appointments), portfolio allocation, expense breakdown, Excel/PDF tax-relevant export.
15. **`PersonalFin_15` Document Vault & Hardening** — attach scanned documents (policy/loan/FD proofs) to records; an explicit Owner-only-access self-check; a `swalekha_db` backup/restore drill, matching the discipline `BS-21` established for the business database.

## 6. Open Items To Confirm During Build (non-blocking)

- Exact default Expense Sheet types beyond Travel/Personal/House/Medical/Hidden — the schema supports user-defined custom types regardless, so this can be settled per-stage rather than up front.
- Mutual Fund/Share current price: manual entry only for v1 — no live market-data provider. A future pluggable price-feed provider (mirroring the GST module's provider-registry pattern from Stage GST-1/2) is a natural later add-on, out of scope for the initial build.
- Reminder delivery: an in-app "upcoming dues" dashboard widget for v1; push/email notification is a possible later add-on.
- Single currency (INR), matching the rest of the platform.

## 7. Verification Bar For `PersonalFin_01`

- Login as Owner reaches the empty Swalekha dashboard at `/swalekha`.
- Login as Admin, PowerUser, SuperAdmin, or any other role is refused (403) at both the API policy and the frontend route guard.
- `swalekha` does not appear in the app switcher/sidebar from any other app; the one profile-menu link renders only for Owner.
- `swalekha_db` is confirmed as a genuinely separate database (distinct connection string, distinct backup dump file per the updated protocol).
- `dotnet build` and the existing backend test suite stay green — no regression to the shared API process that also serves the business platform.

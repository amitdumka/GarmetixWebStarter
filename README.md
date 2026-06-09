# Garmetix Web Starter

This starter converts the supplied MAUI model ZIP into a web-first structure:

```text
backend/
  Garmetix.Domain/          # Sanitized model and enum files from Models.zip
  Garmetix.Infrastructure/  # Repository/storage layer
  Garmetix.Api/             # ASP.NET Core API
frontend/
  garmetix-web/             # Nuxt/Vue app shell
docker-compose.yml
```

## Suggested Architecture

- Backend: ASP.NET Core API.
- Domain: your existing C# model and enum classes, with MAUI-only form attributes removed.
- Database: PostgreSQL with EF Core/Npgsql.
- Frontend: Nuxt 4 with Nuxt UI v4 dashboard and module screens.
- Deployment: Docker Compose for Linux or Mac mini.

## Covered Modules

- Company, StoreGroup, Store
- Billing and purchase invoices
- Inventory, products, stock, brands, UOM, tax
- Accounting vouchers, ledgers, bank, petty cash
- HR, attendance, salary structure, salary payment, payslip
- User access and role-wise permissions
- Import/export placeholder for CSV/Excel workflows

## Run Backend Locally

```bash
cd backend/Garmetix.Api
dotnet run
```

API starts on the ASP.NET default development URL. The Nuxt app exposes a same-origin `/api` proxy and forwards it to `NUXT_API_INTERNAL_BASE`, which defaults to `http://localhost:5080/api`; set `ASPNETCORE_URLS=http://localhost:5080` if needed.

## Database

The backend now uses `GarmetixDbContext` and `EfGarmetixRepository`.

Create/update the PostgreSQL database:

```bash
dotnet ef database update --project backend/Garmetix.Infrastructure --startup-project backend/Garmetix.Api
```

The initial migration is already generated under:

```text
backend/Garmetix.Infrastructure/Data/Migrations
```

## Authentication

The API uses JWT bearer authentication and role policies for each module. Create the first admin through the Nuxt login screen's `First Admin` tab or call:

```bash
curl -X POST http://localhost:5080/api/auth/bootstrap-admin -H "Content-Type: application/json" -d "{\"name\":\"Garmetix Admin\",\"userName\":\"admin\",\"email\":\"admin@garmetix.local\",\"password\":\"change-me\"}"
```

More details are in `backend/Auth-Access-Notes.md`. Forgot/reset password email delivery and one-time reset-token storage are documented in `backend/Password-Reset-Email-Notes.md`.

## First Run Setup

After login, the Nuxt dashboard checks `/api/setup/status`. If company/store defaults are missing, use the `Quick Setup` panel to create the first company, store group, store, product category, product subcategory, and GST tax.

## Public Health Check

Use `/api/health` to confirm the public Nuxt site can reach the backend through the same-origin proxy. With Cloudflare Tunnel, open:

```text
https://YOUR-TUNNEL-URL/api/health
```

If `databaseReady` is `true`, frontend, proxy, API, and database connectivity are working.

## Billing POS

Billing now has its own Nuxt route at `/billing`. The page uses Nuxt UI sales KPI cards, searchable `UTable`, a POS slideover workflow, receipt modal, print/PDF actions, cancel confirmation, and toast feedback. It calls `POST /api/billing/sales`, saves invoice/items/payment in one backend transaction, and updates stock sold quantity. Invoice printing supports A4, A5, 2-inch thermal, 3-inch thermal, customer/office/duplicate copies, reprint mode, and optional signature lines. More details are in `backend/Billing-Notes.md`.

## Purchase Inward

Purchase now has its own Nuxt route at `/purchase`. The page uses Nuxt UI purchase KPI cards, searchable `UTable`, and a purchase inward slideover workflow with item cart totals and toast feedback. It calls `POST /api/purchase/inward`, creates or reuses a vendor, creates missing products when name/barcode are supplied, saves the purchase invoice/items, and increases store stock purchase quantity. More details are in `backend/Purchase-Notes.md`.

## Frontend Routing

The dashboard is now an overview page. Module navigation uses separate Nuxt pages, starting with `/setup`, `/billing`, `/purchase`, `/inventory`, `/vouchers`, `/accounting`, `/petty-cash`, `/hr`, `/payroll`, and `/access`. Other module links have placeholder pages ready for their own list/form workflows.

## Nuxt UI Direction

The frontend is staged for Nuxt 4 + Nuxt UI v4. Stage 1 adds the Nuxt UI module, wraps the app in `UApp`, and uses Nuxt UI dashboard layout components for the shared shell. Stage 2 adds reusable CRUD building blocks for page headers, toolbars, empty states, delete confirmation, form slideovers, and toast feedback. Stage 3 now uses the Nuxt Planner demo as the dashboard direction: grouped navigation, compact KPI cards, dense work tables, progress panels, and recent activity. The full staged migration list is in `Nuxt-UI-Implementation-Stages.md`.

Dark mode is the default theme. Users can switch between Dark, Light, and System from the dashboard top bar, or use the theme icon button for quick toggling.

## Company Setup

Company Setup now has its own Nuxt route at `/setup`. The page manages companies, store groups, and stores with Nuxt UI summary cards, tabbed lists, `UTable`, slideover add/edit forms, toast feedback, and delete confirmation.

## Inventory

Inventory now has its own Nuxt route at `/inventory`. The page uses Nuxt UI stock KPI cards, searchable `UTable`, product add/edit slideover forms, toast feedback, and delete confirmation. It shows purchase quantity, sold quantity, current stock, and MRP stock value.

## Vouchers

Vouchers now have their own Nuxt route at `/vouchers`. The page uses Nuxt UI payment, receipt, and expense KPI cards, searchable `UTable`, voucher add/edit slideover forms, toast feedback, and delete confirmation. Non-cash vouchers require a bank account and post bank transactions, statement lines, and cheque logs where applicable.

## Accounting

Accounting now has its own Nuxt route at `/accounting`. It includes Indian accounting defaults, ledger groups, ledgers, parties, bank accounts, bank transaction entry, bank statements, vendor bank accounts, cheque logs, and trial balance. Voucher save uses double-entry journal posting: payments and expenses debit the selected ledger and credit cash/bank; receipts debit cash/bank and credit the selected ledger.

## Petty Cash

Petty Cash now has its own Nuxt route at `/petty-cash`. The page uses Nuxt UI daily cash KPI cards, searchable `UTable`, cash sheet add/edit slideover forms, calculated cash summary, toast feedback, and delete confirmation.

## HR

HR now has its own Nuxt route at `/hr`. The page uses Nuxt UI KPI cards, tabs, `UTable`, employee and attendance slideover forms, monthly attendance generation controls, toast feedback, and delete confirmation. Monthly attendance can be generated from daily attendance rows, and the HR page auto-runs generation once when opened on the last day of a month.

## Payroll

Payroll now has its own Nuxt route at `/payroll`. The page uses Nuxt UI KPI cards, payslip, salary structure, and salary payment tabs, `UTable`, slideover forms, salary summaries, billable-days attendance context, toast feedback, and delete confirmation.

Payslips can be generated for a selected month from `/api/payroll/payslips/generate-month`. The generator prorates salary from monthly attendance billable days, reduces salary advances, carries previous unpaid due, and shows the remaining due amount. The Payroll page auto-generates the previous month's payslips once when opened on the first day of a month. Each payslip can be opened in a print view and saved as PDF from the browser print dialog, with quick Email and WhatsApp share actions.

Salary payments saved through `/api/salary-payments` now post accounting journals automatically. Net salary payments debit `Salary Payables`; advance payments debit `Salary Advance`; cash payments credit `Cash In Hand`; non-cash payments credit `Bank Clearing`. Editing a salary payment reposts the journal, and deleting it removes the posting.

The API also runs a hosted payroll automation job. On the last day of each month it generates monthly attendance from daily attendance. On the first day of each month it generates payslips for the previous month, including attendance proration, salary advance reduction, and carry-forward due calculation. The job is idempotent: if records already exist, it updates them instead of duplicating them.

Configure the automation in `backend/Garmetix.Api/appsettings.json`:

```json
"PayrollAutomation": {
  "Enabled": true,
  "TimeZoneId": "Asia/Kolkata",
  "RunHour": 2,
  "RunMinute": 0,
  "RunOnStartup": true
}
```

For Linux and Mac mini deployments, `Asia/Kolkata` is recommended. The app also falls back to `India Standard Time` for Windows development machines.

## Access

Access now has its own Nuxt UI route at `/access`. The page manages users, roles, user types, admin access, password reset, and company/store scope with KPI cards, searchable `UTable`, role badges, form slideover, reset dialog, toast feedback, and delete confirmation. It uses safe `/api/access/users` endpoints that hash passwords and do not return password hashes.

## Import Export

Import Export now has its own Nuxt route at `/import-export`. Admin users can download CSV exports and matching import templates for setup, inventory, billing, purchase, vouchers, petty cash, HR, payroll, and access.

Validated CSV import is enabled for setup, inventory, billing, purchase, HR employees, payroll structures/payments, vouchers, petty cash, and access users. Use `Validate CSV` first to check row and field errors, then `Import CSV` to commit valid files. Setup import supports Company, StoreGroup, and Store rows with `CompanyCode` and `StoreGroupCode` parent mapping for multi-store data. Billing import is line-item based, create-only, validates stock availability, creates customers when missing, reduces stock, posts sales ledgers, and creates bank transactions for non-cash paid imports. Purchase import is line-item based and creates purchase invoices, vendors/products when missing, stock inward entries, ledger postings, and bank transactions for non-cash paid imports. Payroll import accepts `Structure` and `Payment` rows; structures feed monthly payslip generation, and payments/advances are included in payable and due calculation. Access import keeps exported passwords blank, requires a password only for new users, and prevents downgrading the last admin.

## Audit

Audit now has its own Nuxt UI route at `/audit`. Admin users can review a searchable activity register generated from existing created/updated timestamps across setup, inventory, billing, purchase, vouchers, petty cash, HR, and payroll. The backend endpoint is `GET /api/audit/recent`.

## Backup and Restore

System Health now includes admin-only full PostgreSQL backup and restore controls. Manual and scheduled backups use PostgreSQL custom dump format, are stored in the host `backups/` directory, can be downloaded or deleted, and survive container replacement. Scheduled backups run daily at 2:30 AM Asia/Kolkata by default and retain the latest 14 scheduled files.

Restore accepts only PostgreSQL custom-format `.dump` files and requires typing `RESTORE`. The API blocks other application requests while restore is running and creates a pre-restore safety backup automatically.

Configure the schedule with the `BACKUP_*` values in `.env.example`. Google Drive synchronization is not enabled by default because it requires a Google account credential and destination-folder authorization.

## Reports

Reports now has its own Nuxt UI route at `/reports`. It includes Sales, Purchase, Stock, Petty Cash, Attendance, and Payroll report views with date filters, KPI cards, searchable `UTable` rows, print action, and client-side CSV export.

## Run Frontend Locally

```bash
cd frontend/garmetix-web
npm install
npm run dev
```

On this Windows machine, if `node` resolves to a blocked WindowsApps shim, use the installed Node.js path explicitly:

```powershell
$env:NODE_OPTIONS='--use-system-ca'
$env:PATH='C:\Program Files\nodejs;' + $env:PATH
& 'C:\Program Files\nodejs\npm.cmd' install
& 'C:\Program Files\nodejs\npm.cmd' run build
```

The Nuxt production build has been verified with this command path.

## Run With Docker

```bash
docker compose up --build
```

Then open:

- Web: `http://localhost:3000`
- API: `http://localhost:5080`

## Windows Docker Operations

Windows helper scripts are available under `scripts/windows`:

```powershell
.\scripts\windows\start-garmetix.ps1 -Build
.\scripts\windows\health-check.ps1 -PublicUrl "https://garmetix.aadwikafashion.in"
.\scripts\windows\backup-db.ps1
```

Use `Deployment-Windows.md` for Windows hosting, Cloudflare Tunnel, restart, backup, and restore steps.

## Production Deploy

For Linux or Mac mini deployment:

```bash
cp .env.example .env
docker compose -f docker-compose.prod.yml up --build -d
```

Set `POSTGRES_PASSWORD` and `JWT_SIGNING_KEY` in `.env` before starting. Keep `NUXT_PUBLIC_API_BASE=/api` unless you intentionally expose the API on a separate public host. Full deployment, backup, restore, and update notes are in `Deployment-Linux-MacMini.md`.

## Migration Notes

EF Core setup details are in `backend/EF-Core-Next-Step.md`.

The model conversion made one bridge change: `AppUser` now implements `IEntity` because it already has an `Id` and should participate in the same API/storage pattern.

Date/time values are normalized before saving so PostgreSQL `timestamp without time zone` columns can accept values submitted from browsers and backend audit stamps.

- Password reset email delivery through configurable SMTP. See `backend/Password-Reset-Email-Notes.md`.


## Workspace permissions

Company / StoreGroup / Store selector is now backed by `/api/workspace/options` and server-side scope checks. See `backend/Workspace-Permissions-Notes.md`.


## Google Drive Online Backup

Google Drive backup is optional and disabled by default. To enable it, create `./secrets/google-drive-service-account.json`, share the target Drive folder with that service account email, set `GOOGLE_DRIVE_BACKUP_ENABLED=true`, and set `GOOGLE_DRIVE_FOLDER_ID` in `.env`. Admin users can view/upload/download/delete/restore cloud backups from **System Health**. See `backend/Google-Drive-Backup-Notes.md`.

### GST Returns standalone module

A separate urgent GST Returns module is available at `/gst-returns`. It currently accepts manual return values and generates:

- GSTR-1 JSON
- GSTR-1 Excel
- GSTR-3B JSON
- GSTR-3B Excel

This module is intentionally not linked to Billing/Purchase yet. See `backend/GST-Return-Notes.md`.



## GST Returns module

Open `/gst-returns` for the standalone manual GST module. It supports GSTR-1 and GSTR-3B JSON/Excel generation, preview validation, and a portal/offline-utility schema review checklist. It is intentionally not linked to Billing/Purchase yet.

- GST saved drafts and audit trail are available in `/gst-returns`. Apply migration `AddGstReturnDrafts` before testing saved drafts.

- Reports page now supports Excel export, Save-as-PDF/print, and local cached report snapshots by report/filter/search.

### GSTIN verification

Customer and Vendor creation now supports GSTIN lookup/validation through a configurable provider. Open `/parties` to verify GSTIN, store legal/trade name and address details, and see mismatch alerts before saving. Billing and Purchase forms also show GSTIN alerts. Configure `GSTIN_LOOKUP_*` values in `.env` before production use. See `backend/GSTIN-Verification-Notes.md`.
- Purchase now has list/detail/print/PDF and cancellation with stock reversal; see `backend/Purchase-Print-Cancel-Notes.md`.


### Latest workflow additions

- Purchase invoices now support **vendor payment voucher** creation from outstanding balance.
- Billing invoices now support **partial sales return / credit note** with selected-item stock reversal and customer store-credit adjustment.

## Latest completion batch

This package also includes:

- Sales exchange flow with linked return credit note and replacement invoice.
- GST Returns Load From Books for GSTR-1 and GSTR-3B.
- Audit filters, entity field details, and CSV export.
- Developer validation checklist under `backend/Developer-Validation-Checklist.md`.

### Runtime migration fix

This package includes a fix for the API restart loop caused by EF Core `PendingModelChangesWarning` during startup auto-migration. See `backend/Runtime-Migration-Fix-Notes.md`.

## Latest commercial workflow additions

- Debit/Credit Note module: `/commercial-notes`
- Customer advance receipts: `/commercial-notes`
- Loyalty program setup: `/loyalty`
- Product barcode/autocomplete lookup API: `/api/product-lookup`
- Document scan lookup API: `/api/scan/{code}`
- Printable invoice/voucher/note PDFs include a scan code for quick lookup.

Run migrations after this update:

```bash
dotnet ef database update --project backend/Garmetix.Infrastructure --startup-project backend/Garmetix.Api
```

### Runtime schema drift repair

If an older PostgreSQL Docker volume is missing newer Customer/Vendor GSTIN columns, the API now runs a safe idempotent repair after auto-migration. See `backend/Database-Schema-Drift-Fix-Notes.md`.

### Customer / Debit-Credit Note / Loyalty refinement

This package separates Debit Note and Credit Note into their own module pages and keeps `/commercial-notes` as a register/summary only. It also adds a dedicated `/customers` module with create/edit form routes, GST status, credit balance, loyalty balance, and customer loyalty ledger links. Loyalty management now supports customer selection and manual point adjustment/redeem ledger entries.

If an existing PostgreSQL volume is missing newer runtime tables, the API now performs defensive schema repair before Audit, Commercial Notes, Customer Advance, and Loyalty queries. Admins can also run `POST /api/database/repair`.

## Latest: GST Accounting Integration

The GST Returns module is now connected with accounting service posting while keeping manual GST draft/export separate:

- GST accounting summary from `Output GST` and `Input GST` ledgers.
- GSTR-3B settlement journal posting.
- Saved GSTR-3B draft can be posted to accounting with audit entry.
- Startup schema repair now runs even when auto-migration is disabled.
- Project tracking now uses `CodexTODO.md` for requested features and `CodexISSUES.md` for bugs/errors.

See `backend/GST-Accounting-Integration-Notes.md`.


## Oracle Cloud Secondary Sync

The API includes an admin-only Oracle secondary sync module for using Oracle Cloud as a shared hub database while PostgreSQL remains the primary Garmetix database.

- UI: `http://localhost:3000/oracle-sync`
- Status: `GET /api/oracle-sync/status`
- Test connection: `POST /api/oracle-sync/test`
- Repair local/Oracle sync tables: `POST /api/oracle-sync/repair`
- Manual run: `POST /api/oracle-sync/run`

Configure with `ORACLE_SYNC_*` values in `.env`. See `backend/Oracle-Secondary-Sync-Notes.md`.

### Oracle Sync v2

Oracle secondary sync now supports safe bidirectional mode. Garmetix still treats PostgreSQL as the primary transaction store. External Oracle events are pulled into `OracleSyncInboundEvents` and failed/unsupported events go to `OracleSyncDeadLetters` until entity ownership and merge rules are approved.

See `backend/Oracle-Secondary-Sync-v2-Notes.md`.

### Oracle Secondary Sync v3

Oracle Sync now includes an entity ownership matrix and explicit inbound apply/reject actions. Open `/oracle-sync` as an admin to review Oracle inbound events, apply shared master data, reject unwanted events, and inspect ownership rules.

New endpoints:

- `GET /api/oracle-sync/ownership`
- `POST /api/oracle-sync/inbound/{id}/apply`
- `POST /api/oracle-sync/inbound/{id}/reject`

Shared master entities can be applied after review. Transactional, GST, stock, loyalty-ledger, and accounting entities remain blocked by default unless force-applied by admin.

## Oracle Sync v4 - Readiness and trusted auto-apply

Oracle Sync now includes a cloud-readiness panel and a trusted auto-apply policy for shared master data. PostgreSQL remains the primary transactional database. Oracle Cloud Free Tier can be used as a common hub for other apps.

Auto-apply is intentionally allowlist based. Configure `ORACLE_SYNC_AUTO_APPLY_ENTITIES` and `ORACLE_SYNC_TRUSTED_SOURCES`, then keep `ORACLE_SYNC_APPLY_INBOUND_AUTOMATICALLY=false` until manual pull/apply testing is complete.

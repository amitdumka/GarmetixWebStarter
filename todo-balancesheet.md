# GarmetixWebStarter — Final Accounts Module TODO

**File:** `todo-balancesheet.md`  
**Branch:** `balancesheet`  
**Merge target:** `version6`  
**Rule:** This file is the execution source of truth. Update it after every completed task and commit.

---

## Status legend

- `[ ]` Not started.
- `[-]` In progress.
- `[x]` Completed and verified.
- `[!]` Blocked or failed.
- `[~]` Deferred with documented reason.

For every completed stage, add:

- date;
- agent;
- commit hash;
- commands/tests run;
- result;
- unresolved issues;
- files added/changed.

Do not mark a stage complete based only on generated code.

---

# BS-00 — Branch, baseline and documentation

## Branch protection

- [x] Confirm working tree status.
- [x] Preserve any existing uncommitted work.
- [x] Fetch remote branches.
- [x] Confirm `version6` exists.
- [x] Update local `version6` using fast-forward only.
- [x] Create or switch to `balancesheet`.
- [x] Confirm active branch using `git branch --show-current`.
- [~] Push feature branch with upstream only after checking CI does not deploy feature branches.
- [x] Confirm no command will affect the deployed server.
- [x] Confirm production Docker/Cloudflare configuration is untouched.

## Baseline discovery

- [x] Record solution/project paths.
- [x] Record backend startup/module registration path.
- [x] Record EF DbContext and migration pattern.
- [x] Record tenant/company/store base entity conventions.
- [x] Record permission registration pattern.
- [x] Record sidebar/menu registration pattern.
- [x] Record Nuxt page/layout pattern.
- [x] Record API client/composable pattern.
- [x] Record test projects and commands.
- [x] Record HR/POS module patterns to copy.
- [x] Record current source tables/endpoints for Sales, Purchase, Inventory, GST, Vouchers, Payroll and Day Book.
- [x] Confirm whether an outbox/event table already exists.
- [x] Confirm current feature/settings storage mechanism.

## Baseline validation

- [x] Backend restore succeeds.
- [x] Backend build succeeds.
- [x] Backend tests run and result recorded.
- [x] Frontend dependency install/lockfile state checked.
- [!] Frontend typecheck/lint runs according to project scripts.
- [x] Frontend production build succeeds.
- [x] Existing failures are documented before module work.
- [x] `roadmap-balancesheet.md` is present.
- [x] `todo-balancesheet.md` is present.
- [x] Commit BS-00.

Evidence:

```text
Date: 2026-07-13
Agent: Codex GPT-5
Branch: balancesheet
Worktree: C:\AIarea\Codex\bsheet\GarmetixWebStarter
Base workspace preserved: C:\AIarea\Codex\GarmetixWebStarter stayed on version6
Baseline commit: 4d3c2b5c13b99fed0237203087d3702b13fd2608

Route decision:
- Use /final-accounts and /api/final-accounts, not /accounts or /detailed-accounts.
- Reason: the module scope is wider than ledgers/accounts and includes Balance Sheet, P&L, Cash Flow, projections, CA review, close, Tally export and reconciliation.
- Corrected branch spelling from the handoff typo to balancesheet.

Discovery:
- No .sln file found. Backend projects: backend/Garmetix.Api/Garmetix.Api.csproj, backend/Garmetix.Domain/Garmetix.Domain.csproj, backend/Garmetix.Infrastructure/Garmetix.Infrastructure.csproj, backend/Garmetix.Api.Tests/Garmetix.Api.Tests.csproj.
- Backend startup/module registration: backend/Garmetix.Api/Program.cs registers services and endpoint modules through app.Map*Endpoints plus generic MapCrud registrations.
- Infrastructure registration: backend/Garmetix.Infrastructure/DependencyInjection.cs adds shared GarmetixDbContext with Npgsql; no retrying execution strategy by default because existing write endpoints use explicit transactions.
- EF/migrations: shared backend/Garmetix.Infrastructure/Data/GarmetixDbContext.cs and migrations under backend/Garmetix.Infrastructure/Data/Migrations. Design-time factory uses GARMETIX_CONNECTION_STRING or local garmetix defaults.
- Base entities: BaseEntity has Id, CreatedAt, UpdatedAt, Synced, Deleted. CompanyBase adds CompanyId and CreatedBy. GroupBase adds StoreGroupId. StoreBase adds StoreId. WorkspaceScope applies claim-based CompanyId/StoreGroupId/StoreId query and write guards.
- Permissions: backend/Garmetix.Api/Auth/GarmetixPolicies.cs plus AccessPermissionMatrix.cs. Current policies include Billing, Inventory, Purchase, Accounting, Hr, Payroll, Attendance, Marketing and Gst.
- Frontend modules: frontend/modular/apps contains main, pos, hr, ai-sense, books, crm, admin and inventory. Shared app registry is frontend/modular/config/apps.ts; route/menu registry is frontend/modular/config/routes.ts.
- Sidebar/layout: frontend/modular/packages/shared-ui/components/ModularAppShell.vue provides the common shell, app switcher, navigation and auth-aware route rendering.
- Nuxt/API client pattern: module apps use workspace packages under frontend/modular/packages. Books uses frontend/modular/apps/books/utils/books-api.ts on top of createGarmetixApiClient from @garmetix/shared-api.
- HR/POS patterns: isolated Nuxt apps under frontend/modular/apps/hr and frontend/modular/apps/pos, dedicated pages and package.json dev/build/preview scripts, registered in apps.ts/routes.ts.
- Tests/build commands: dotnet test backend/Garmetix.Api.Tests/Garmetix.Api.Tests.csproj -c Release; dotnet build backend/Garmetix.Api/Garmetix.Api.csproj -c Release; frontend/modular scripts include check, validate, build:main, build:books and module readiness scripts.
- Source API areas: Billing, Purchase, PurchaseImport, Inventory, GstReturns/GstTax/Gstin, Accounting/OffBook vouchers, Payroll/Hr/Attendance, StoreDay/CashDetails, DayBook and Reports.
- Source tables/DbSets include SalesInvoices, InvoiceItems, InvoicePayments, CardPayments, PurchaseInvoices, PurchaseInvoiceItems, PurchasePayments, PurchaseReturns, VendorSettlements, StockMovements, StockOperationDocuments, GstReturnDrafts, GstApiCallLogs, Vouchers, CashVouchers, SalaryPaySlips, SalaryPayments, DayBegins, DayEnds and CashDetails.
- Outbox/event support: no generic outbox table found. Existing queue/event-like tables include DotMatrixPrintQueueEntries, DigitalInvoiceEvents, WhatsAppMessageLogs, ApplicationMessageLog service/logging, and AttendanceKioskSyncBatch. Final Accounts should start with its own sync cursor/exception queue unless a source-specific event is proven safe.
- Settings storage: mixed pattern. Technical feature gates use typed Options from configuration; persisted module settings exist as domain tables such as DotMatrixPrintSettings, WhatsAppProviderSettings and GstApiProvider/Feature. SystemDefaultsService seeds accounting defaults through /api/setup/accounting-defaults.
- CI/CD: no .github workflow folder found in this worktree. Deployment scripts exist under frontend/modular/deploy and legacy/deploy; none were executed.

Build commands:
- dotnet restore backend/Garmetix.Api/Garmetix.Api.csproj: passed.
- dotnet build backend/Garmetix.Api/Garmetix.Api.csproj -c Release: passed with existing nullable warnings.
- npm ci in frontend/modular: failed because package-lock.json is out of sync with package.json after inventory workspace addition.
- npm install --package-lock=false in frontend/modular: passed for local validation only; no tracked lockfile change.
- npm run check in frontend/modular: passed.
- npm run validate in frontend/modular: failed at existing Main Back Office contract parity.
- npm run build:books in frontend/modular: passed with Nuxt/Rollup warnings.
- npm run build:main in frontend/modular: passed with Nuxt/Rollup warnings.

Test commands:
- dotnet test backend/Garmetix.Api.Tests/Garmetix.Api.Tests.csproj -c Release --no-restore: failed before restore because backend/Garmetix.Api.Tests/obj/project.assets.json was missing in the fresh worktree.
- dotnet test backend/Garmetix.Api.Tests/Garmetix.Api.Tests.csproj -c Release: passed. Result: 81 passed, 3 skipped, 84 total.

Results:
- Isolated balancesheet worktree created from latest version6.
- Governing docs added from handoff zip and branch spelling corrected to balancesheet.
- Baseline backend restore/build/tests pass after restoring the test project.
- Frontend clean install has a baseline lockfile mismatch; no tracked package file was changed in BS-00.
- Main and Books production builds pass after no-lock local install.

Known baseline failures:
- frontend/modular npm ci fails: package-lock.json missing @garmetix/inventory-web@6.0.22.
- frontend/modular npm run validate fails: Main Back Office contract parity reports frontend/modular/apps/main/pages/purchase/index.vue missing token purchase/invoices/recent.
- API build has existing nullable warnings in BankReconciliationClosureEndpoints.cs, BillingEndpoints.cs, VyaparSaleImportService.cs, DigitalBillCrmEndpoints.cs and PurchaseEndpoints.cs.
- Nuxt builds show existing warnings about sourcemaps, Rollup pure comments, large chunks and unresolved nitro cache-driver external treatment.

BS-00 commit: created by this stage commit; exact hash is reported in session output because a commit cannot contain its own final hash.
```

---

# BS-01 — Module skeleton and feature flag

## Backend

- [x] Create dedicated Final Accounts namespace/folder.
- [x] Create module registration extension.
- [x] Create module status endpoint.
- [x] Add typed module settings.
- [x] Persist module settings using existing setup/settings system.
- [x] Default `FinalAccounts.Enabled` to false.
- [x] Add module-disabled guard for APIs.
- [x] Add permission constants.
- [x] Add permission seeds without auto-granting normal roles.
- [x] Add structured logging category.
- [x] Add health information without operational posting.

## Frontend

- [x] Create `/final-accounts/` dashboard shell.
- [x] Use normal application layout/header/sidebar.
- [x] Add module-disabled page/state.
- [x] Add permission guard.
- [x] Add sidebar group hidden while disabled.
- [x] Add setup route for authorised users.
- [x] Add empty/loading/error states.
- [x] Verify no existing route/menu regression.

## Tests

- [x] API rejects disabled-module access.
- [x] Authorised setup user can view status.
- [x] Menu is absent when disabled.
- [x] Existing users have no new access by default.
- [!] Existing application tests/builds pass.
- [x] Commit BS-01.

Evidence:

```text
Date: 2026-07-13
Agent: Codex GPT-5
Branch: balancesheet
Worktree: C:\AIarea\Codex\bsheet\GarmetixWebStarter
Commit: created by this stage commit; exact hash is reported in session output because a commit cannot contain its own final hash.

Implementation:
- Backend Final Accounts module added under backend/Garmetix.Api/FinalAccounts.
- Domain settings model added under backend/Garmetix.Domain/Generated/Models/FinalAccounts.
- Additive migration added for PostgreSQL schema final_accounts and table final_accounts.fa_module_settings.
- Module settings are persisted in fa_module_settings and default to disabled.
- API root: /api/final-accounts.
- Frontend route/app root: /final-accounts.
- Endpoints added: GET /api/final-accounts/status, GET /api/final-accounts/settings, PUT /api/final-accounts/settings, guarded GET /api/final-accounts/dashboard.
- Disabled guard returns HTTP 403 for guarded module endpoints while settings are disabled.
- Policy GarmetixPolicies.FinalAccounts added and registered, but not added to AccessPermissionMatrix module-role grants.
- Admin/owner retain setup access through the existing IsAdminOrOwner policy path.
- Existing non-admin roles do not receive Final Accounts access by default.
- Structured service log added when Final Accounts settings are saved.
- Dedicated Nuxt app added at frontend/modular/apps/final-accounts with status, setup, login and access-denied pages.
- Shared app registry, route registry, app switcher labels, shell defaults, env sample and workspace-link checks updated.
- Shared menu route records are showInMenu:false for BS-01.
- No production deployment script was added or executed.
- No production migration was run.

Commands/tests run:
- dotnet build backend/Garmetix.Api/Garmetix.Api.csproj -c Release: passed with existing nullable warnings.
- dotnet test backend/Garmetix.Api.Tests/Garmetix.Api.Tests.csproj -c Release: passed. Result: 88 passed, 3 skipped, 91 total.
- npm install --package-lock=false in frontend/modular: passed for local workspace-link refresh; no tracked package-lock change.
- npm run check in frontend/modular: passed.
- npm run workspace-links in frontend/modular: passed.
- npm run final-accounts:readiness in frontend/modular: passed.
- npm --workspace @garmetix/final-accounts-web run build: passed with existing Nuxt/Rollup warning pattern.
- npm run build:books in frontend/modular: passed with existing Nuxt/Rollup warning pattern.
- npm run build:main in frontend/modular: passed with existing Nuxt/Rollup warning pattern.
- npm run build:admin in frontend/modular: passed with existing Nuxt/Rollup warning pattern.
- npm run validate in frontend/modular: failed at existing Main Back Office contract parity baseline.

Known unresolved issues:
- frontend/modular npm run validate still fails because frontend/modular/apps/main/pages/purchase/index.vue is missing token purchase/invoices/recent.
- npm ci remains expected to fail until package-lock.json is refreshed for the existing workspace app additions; BS-01 did not update package-lock.json.
- API build still reports existing nullable warnings in BankReconciliationClosureEndpoints.cs, BillingEndpoints.cs, VyaparSaleImportService.cs, DigitalBillCrmEndpoints.cs and PurchaseEndpoints.cs.
- Nuxt builds still report existing sourcemap, Rollup pure-comment, large chunk and nitro cache-driver warnings.

Files added/changed:
- backend/Garmetix.Api/FinalAccounts/*
- backend/Garmetix.Domain/Generated/Models/FinalAccounts/FinalAccountsModuleSettings.cs
- backend/Garmetix.Infrastructure/Data/Migrations/20260713153000_AddFinalAccountsModuleSettings.cs
- backend/Garmetix.Infrastructure/Data/GarmetixDbContext.cs
- backend/Garmetix.Api/Auth/GarmetixPolicies.cs
- backend/Garmetix.Api/Program.cs
- backend/Garmetix.Api.Tests/Auth/AccessPermissionMatrixTests.cs
- frontend/modular/apps/final-accounts/*
- frontend/modular/config/apps.ts
- frontend/modular/config/routes.ts
- frontend/modular/packages/shared-types/src/index.ts
- frontend/modular/packages/shared-ui/src/index.ts
- frontend/modular/packages/shared-ui/components/ModularAppShell.vue
- frontend/modular/.env.example
- frontend/modular/package.json
- frontend/modular/scripts/validate-structure.mjs
- frontend/modular/scripts/workspace-link-readiness.mjs
- frontend/modular/scripts/final-accounts-readiness.mjs
```

---

# BS-02 — Database schema, fiscal periods and Chart of Accounts

## Database

- [x] Add PostgreSQL schema `final_accounts`.
- [~] Add Final Accounts migration history configuration if separate DbContext is used.
- [x] Add `fa_module_settings`.
- [x] Add `fa_account_groups`.
- [x] Add `fa_accounts`.
- [x] Add `fa_account_mappings`.
- [x] Add `fa_fiscal_years`.
- [x] Add `fa_fiscal_periods`.
- [x] Add audit columns/concurrency tokens.
- [x] Add unique account code constraints.
- [x] Add hierarchy validation.
- [x] Verify migration is additive.
- [x] Verify migration does not alter/drop operational tables.
- [~] Test clean migration.
- [~] Test migration against an existing test database.

## Domain/API

- [x] Account type and natural-balance enums.
- [x] Account group CRUD.
- [x] Account CRUD.
- [x] Fiscal year CRUD.
- [x] Period state transitions.
- [x] Default garment-retail COA seed preview.
- [x] Explicit user approval before seeding.
- [x] Duplicate and circular hierarchy validation.
- [x] Protected system-account rules.
- [x] Search/autocomplete endpoint.

## UI

- [x] Chart of Accounts tree/list.
- [x] Group create/edit.
- [x] Account create/edit.
- [x] Account filters and autocomplete.
- [x] Fiscal year/period page.
- [x] Seed preview and result.
- [x] Validation summary.

## Tests

- [~] Tenant isolation.
- [~] Duplicate code rejection.
- [x] Natural-balance validation.
- [x] Circular group rejection.
- [x] Period date overlap rejection.
- [~] Permission tests.
- [x] Commit BS-02.

Evidence:

```text
Date: 2026-07-13
Agent: Codex GPT-5
Branch: balancesheet
Worktree: C:\AIarea\Codex\bsheet\GarmetixWebStarter
Commit: created by this stage commit; exact hash is reported in session output because a commit cannot contain its own final hash.

Implementation:
- Added Final Accounts catalog and fiscal domain models under backend/Garmetix.Domain/Generated/Models/FinalAccounts.
- Added additive migration 20260713162000_AddFinalAccountsCatalogAndFiscalPeriods for final_accounts.fa_account_groups, fa_accounts, fa_account_mappings, fa_fiscal_years and fa_fiscal_periods.
- Added EF migration metadata attributes to the BS-01 and BS-02 manual migrations so dotnet ef list/script can discover them.
- The migration creates only final_accounts schema objects and final_accounts-only foreign keys; no operational tables are altered, renamed or dropped.
- Added DbSet and model mapping for catalog/fiscal entities in the shared GarmetixDbContext.
- Added FinalAccountsCatalogService with scoped CRUD, WorkspaceScope write checks, duplicate-code checks, natural-balance checks, circular hierarchy checks, fiscal overlap checks, status transition rules, protected system-row rules, search and validation summary.
- Added endpoints behind the existing FinalAccounts policy and enabled-module filter:
  GET/POST/PUT/DELETE /api/final-accounts/account-groups,
  GET/POST/PUT/DELETE /api/final-accounts/accounts,
  GET /api/final-accounts/accounts/search,
  GET/POST/PUT/DELETE /api/final-accounts/account-mappings,
  GET/POST/PUT /api/final-accounts/fiscal-years,
  GET/POST/PUT /api/final-accounts/fiscal-periods,
  GET /api/final-accounts/coa/seed-preview,
  GET /api/final-accounts/validation/summary.
- Seed endpoint is preview-only in BS-02; no seeding mutation was added, so seeding still requires a future explicit approval/action.
- Added Final Accounts pages:
  /chart-of-accounts with group/account/mapping forms, tables, filters, seed preview and validation metrics.
  /fiscal-periods with fiscal year and period forms, status badges and validation metrics.
- Added shell menu links and hidden route registry entries for the new pages.
- Updated Final Accounts API client types and post/delete helpers.
- Updated modular structure and Final Accounts readiness checks for BS-02.
- No production migration was run.
- No production deployment script or Cloudflare/Docker/server configuration was changed.

Commands/tests run:
- dotnet build backend/Garmetix.Api/Garmetix.Api.csproj -c Release: passed with existing nullable warnings.
- dotnet test backend/Garmetix.Api.Tests/Garmetix.Api.Tests.csproj -c Release: passed. Result: 97 passed, 3 skipped, 100 total.
- dotnet ef migrations list --project backend/Garmetix.Infrastructure/Garmetix.Infrastructure.csproj --startup-project backend/Garmetix.Api/Garmetix.Api.csproj --context GarmetixDbContext --configuration Release --no-build: passed and listed 20260713153000_AddFinalAccountsModuleSettings and 20260713162000_AddFinalAccountsCatalogAndFiscalPeriods; local PostgreSQL connection was unavailable so applied/pending status could not be checked.
- dotnet ef migrations script 20260713153000_AddFinalAccountsModuleSettings 20260713162000_AddFinalAccountsCatalogAndFiscalPeriods --project backend/Garmetix.Infrastructure/Garmetix.Infrastructure.csproj --startup-project backend/Garmetix.Api/Garmetix.Api.csproj --context GarmetixDbContext --configuration Release --no-build: passed; generated SQL only creates final_accounts catalog/fiscal objects and inserts the BS-02 migration history row.
- npm run check in frontend/modular: passed.
- npm run workspace-links in frontend/modular: passed.
- npm run final-accounts:readiness in frontend/modular: passed.
- npm --workspace @garmetix/final-accounts-web run build: passed with existing Nuxt/Rollup warning pattern.
- npm run build:books in frontend/modular: passed with existing Nuxt/Rollup warning pattern.
- npm run build:main in frontend/modular: passed with existing Nuxt/Rollup warning pattern.
- npm run build:admin in frontend/modular: passed with existing Nuxt/Rollup warning pattern.
- npm run validate in frontend/modular: failed at existing Main Back Office contract parity baseline.

Known unresolved issues:
- No live clean/existing PostgreSQL migration application was run because localhost:5432 was unavailable.
- Tenant isolation, duplicate-code rejection and permission behavior are implemented in service/policy paths but do not yet have database-backed integration tests in BS-02.
- frontend/modular npm run validate still fails because frontend/modular/apps/main/pages/purchase/index.vue is missing token purchase/invoices/recent.
- npm ci remains expected to fail until package-lock.json is refreshed for the existing workspace app additions; BS-02 did not update package-lock.json.
- API build still reports existing nullable warnings in BankReconciliationClosureEndpoints.cs, BillingEndpoints.cs, VyaparSaleImportService.cs, DigitalBillCrmEndpoints.cs and PurchaseEndpoints.cs.
- Nuxt builds still report existing sourcemap, Rollup pure-comment, large chunk and nitro cache-driver warnings.

Files added/changed:
- backend/Garmetix.Api/FinalAccounts/FinalAccountsCatalogContracts.cs
- backend/Garmetix.Api/FinalAccounts/FinalAccountsCatalogRules.cs
- backend/Garmetix.Api/FinalAccounts/FinalAccountsCatalogService.cs
- backend/Garmetix.Api/FinalAccounts/FinalAccountsEndpoints.cs
- backend/Garmetix.Api/Program.cs
- backend/Garmetix.Api.Tests/FinalAccounts/FinalAccountsCatalogRulesTests.cs
- backend/Garmetix.Domain/Generated/Models/FinalAccounts/FinalAccountsCatalog.cs
- backend/Garmetix.Infrastructure/Data/GarmetixDbContext.cs
- backend/Garmetix.Infrastructure/Data/Migrations/20260713153000_AddFinalAccountsModuleSettings.cs
- backend/Garmetix.Infrastructure/Data/Migrations/20260713162000_AddFinalAccountsCatalogAndFiscalPeriods.cs
- frontend/modular/apps/final-accounts/pages/chart-of-accounts.vue
- frontend/modular/apps/final-accounts/pages/fiscal-periods.vue
- frontend/modular/apps/final-accounts/pages/index.vue
- frontend/modular/apps/final-accounts/utils/final-accounts-api.ts
- frontend/modular/config/routes.ts
- frontend/modular/packages/shared-ui/components/ModularAppShell.vue
- frontend/modular/scripts/final-accounts-readiness.mjs
- frontend/modular/scripts/validate-structure.mjs
```

---

# BS-03 — General Ledger

## Persistence

- [ ] Add `fa_journal_entries`.
- [ ] Add `fa_journal_lines`.
- [ ] Add `fa_source_posting_links`.
- [ ] Add idempotency unique index.
- [ ] Add debit/credit database checks.
- [ ] Add account/date/source indexes.
- [ ] Add reversal links.
- [ ] Add immutable posted-state rules at service layer.

## Domain/service

- [ ] Journal number generator.
- [ ] Draft creation.
- [ ] Journal validation.
- [ ] Balanced posting transaction.
- [ ] Explicit debit/credit lines.
- [ ] Rounding policy.
- [ ] Fiscal period validation.
- [ ] Manual-posting restrictions.
- [ ] Idempotency handling.
- [ ] Reversal service.
- [ ] Audit events.
- [ ] Optimistic concurrency.
- [ ] No hard delete for posted journals.

## API/UI

- [ ] Journal list with server pagination.
- [ ] Journal detail.
- [ ] New manual adjustment journal for permitted users.
- [ ] Validation preview.
- [ ] Post action.
- [ ] Reverse action with reason.
- [ ] Source reference display.
- [ ] Audit history display.

## Tests

- [ ] Balanced entry posts.
- [ ] Unbalanced entry fails.
- [ ] Both debit and credit on one line fail.
- [ ] Negative amount fails.
- [ ] Duplicate idempotency key returns existing result.
- [ ] Locked period fails.
- [ ] Reversal nets original to zero.
- [ ] Posted journal cannot be edited/deleted.
- [ ] Concurrent post does not duplicate.
- [ ] Commit BS-03.

---

# BS-04 — Posting rules and mappings

## Rule framework

- [ ] Add posting-rule definitions and versions.
- [ ] Add account-mapping configuration.
- [ ] Add posting preview contract.
- [ ] Add adapter interface.
- [ ] Add missing-mapping diagnostics.
- [ ] Add source hash/version support.
- [ ] Add posting-rule audit/version display.
- [ ] Add no-silent-suspense rule.

## Mapping UI

- [ ] Payment mode mapping.
- [ ] Bank/UPI/card clearing mapping.
- [ ] Sales category mapping.
- [ ] Product inventory/COGS mapping.
- [ ] GST component mapping.
- [ ] Expense category mapping.
- [ ] Payroll component mapping.
- [ ] Stock adjustment reason mapping.
- [ ] Discount/rounding mapping.
- [ ] Inter-store clearing mapping.
- [ ] Validation page.

## Tests

- [ ] Missing mapping produces preview error.
- [ ] Mapping version is captured on journal.
- [ ] Invalid control account mapping fails.
- [ ] Mapping changes do not mutate historical journals.

---

# BS-04A — Cash, bank, receipt, payment and expense adapters

- [ ] Cash receipt adapter.
- [ ] Customer receipt adapter.
- [ ] Vendor payment adapter.
- [ ] General payment adapter.
- [ ] Contra transfer adapter.
- [ ] Expense adapter.
- [ ] Input tax handling.
- [ ] TDS payable handling where source supports it.
- [ ] Mixed bank/cash allocation.
- [ ] Cancel/revise/reversal handling.
- [ ] Fixture tests.
- [ ] Reconciliation controls.
- [ ] Commit BS-04A.

---

# BS-04B — Sales and sales-return adapters

- [ ] Cash sale.
- [ ] Credit sale.
- [ ] UPI/card/bank sale.
- [ ] Mixed payment sale.
- [ ] Customer receivable.
- [ ] Taxable value.
- [ ] CGST/SGST.
- [ ] IGST.
- [ ] discount presentation.
- [ ] delivery/other charges.
- [ ] sale return/credit note.
- [ ] revised/cancelled invoice.
- [ ] item/category/store dimensions.
- [ ] Source totals reconcile.
- [ ] Fixture tests.
- [ ] Commit BS-04B.

---

# BS-04C — Purchase and purchase-return adapters

- [ ] Credit purchase.
- [ ] Cash/bank purchase.
- [ ] Inventory/direct expense.
- [ ] Input CGST/SGST/IGST.
- [ ] Freight and landed-cost policy.
- [ ] Vendor payable.
- [ ] TDS where applicable.
- [ ] Purchase return.
- [ ] Supplier debit/credit note.
- [ ] Vendor advance settlement.
- [ ] revised/cancelled inward/invoice.
- [ ] Source totals reconcile.
- [ ] Fixture tests.
- [ ] Commit BS-04C.

---

# BS-04D — Inventory and COGS

- [ ] Confirm perpetual versus periodic method.
- [ ] Document chosen method.
- [ ] Sale COGS posting.
- [ ] Sale-return stock restoration.
- [ ] Purchase inventory posting.
- [ ] Purchase-return inventory reversal.
- [ ] Stock adjustment.
- [ ] Transfer/inter-store clearing.
- [ ] Negative stock exception.
- [ ] Missing-cost exception.
- [ ] Weighted Average support.
- [ ] FIFO support if source data permits.
- [ ] Prevent duplicate closing-stock effect.
- [ ] Reconcile quantity/value.
- [ ] Commit BS-04D.

---

# BS-04E — Payroll, GST/TDS and other adapters

- [ ] Payroll finalisation.
- [ ] Salary payable.
- [ ] Salary payment.
- [ ] Employer/statutory liabilities.
- [ ] GST payment/adjustment.
- [ ] TDS payable/payment.
- [ ] Tailoring and alteration income.
- [ ] Customer/vendor advances.
- [ ] Other income.
- [ ] Fixture tests.
- [ ] Commit BS-04E.

---

# BS-05 — Sync, historical backfill and reconciliation

## Sync

- [ ] Detect/use existing outbox, or implement Final Accounts sync cursor.
- [ ] Add `fa_sync_jobs`.
- [ ] Add job items/checkpoints.
- [ ] Add retry policy.
- [ ] Add failure/exception queue.
- [ ] Ensure operational transactions are never blocked.
- [ ] Ensure job is disabled by default.
- [ ] Add manual sync action.
- [ ] Add safe scheduled mode configuration.

## Backfill

- [ ] Dry-run endpoint.
- [ ] Date/store/company/module filters.
- [ ] Preview counts and totals.
- [ ] Idempotency.
- [ ] Resume checkpoint.
- [ ] Stop/continue error policy.
- [ ] Source content hash.
- [ ] Drift detection.
- [ ] No source writes.
- [ ] No production run.
- [ ] Purpose-built dev cleanup for unapproved migration batch only.

## Reconciliation

- [ ] Sales control totals.
- [ ] Purchase control totals.
- [ ] Receipt/payment totals.
- [ ] Tax totals.
- [ ] Inventory/COGS totals.
- [ ] Payroll totals.
- [ ] Exception categories.
- [ ] Dashboard and export.
- [ ] Tests for rerun and partial failure.
- [ ] Commit BS-05.

---

# BS-06 — General Ledger report and Trial Balance

## General Ledger

- [ ] Opening balance.
- [ ] Period movement.
- [ ] Running balance.
- [ ] Account/store/date filters.
- [ ] Dimensions.
- [ ] Source drill-down.
- [ ] Journal drill-down.
- [ ] Reversal indicators.
- [ ] Server pagination.
- [ ] Excel/PDF export.

## Trial Balance

- [ ] Group view.
- [ ] Ledger view.
- [ ] Opening debit/credit.
- [ ] Period debit/credit.
- [ ] Closing debit/credit.
- [ ] Zero-balance toggle.
- [ ] Monthly/quarterly comparison.
- [ ] Store and company consolidated.
- [ ] Difference diagnostic.
- [ ] Golden fixture.
- [ ] Total debit equals total credit.
- [ ] Commit BS-06.

---

# BS-07 — Statement template engine and Profit & Loss

## Template engine

- [ ] Template.
- [ ] Template version.
- [ ] Hierarchical nodes.
- [ ] Account/group mappings.
- [ ] Formula/subtotal nodes.
- [ ] Sign/display rules.
- [ ] Current/previous/variance.
- [ ] Percentage of sales.
- [ ] Notes/schedule references.
- [ ] Rounding unit.
- [ ] Hide-zero.
- [ ] Drill-down.
- [ ] Mapping validation.

## P&L

- [ ] Revenue.
- [ ] Sales returns/discount.
- [ ] Opening inventory policy.
- [ ] Purchases/direct cost.
- [ ] Closing inventory.
- [ ] COGS.
- [ ] Gross profit.
- [ ] Other income.
- [ ] Operating expenses.
- [ ] EBITDA.
- [ ] Depreciation.
- [ ] Finance cost.
- [ ] Profit before tax.
- [ ] Tax/provision.
- [ ] Profit after tax.
- [ ] Horizontal view.
- [ ] Vertical view.
- [ ] Comparative periods.
- [ ] Export.
- [ ] Golden tests.
- [ ] Commit BS-07.

---

# BS-08 — Balance Sheet, Cash Flow and schedules

## Balance Sheet

- [ ] Non-corporate/proprietorship template.
- [ ] Partnership/LLP template support.
- [ ] Company template support when applicable.
- [ ] Current/non-current classification.
- [ ] Assets.
- [ ] Equity/capital.
- [ ] Liabilities.
- [ ] Current-year profit transfer.
- [ ] Comparative figures.
- [ ] Consolidation.
- [ ] Inter-store clearing identification.
- [ ] Assets equal equity plus liabilities.
- [ ] Export and drill-down.
- [ ] Golden tests.

## Cash Flow

- [ ] Indirect method.
- [ ] Non-cash adjustments.
- [ ] Working-capital changes.
- [ ] Operating activities.
- [ ] Investing activities.
- [ ] Financing activities.
- [ ] Cash-equivalent mapping.
- [ ] Opening-to-closing reconciliation.
- [ ] Golden tests.

## Schedules

- [ ] Debtor ageing.
- [ ] Creditor ageing.
- [ ] Inventory schedule.
- [ ] Fixed asset/depreciation schedule.
- [ ] Cash and bank.
- [ ] GST/TDS.
- [ ] Loans.
- [ ] Capital.
- [ ] Notes and attachments.
- [ ] Commit BS-08.

---

# BS-09 — CA workspace

- [ ] Adjustment batch CRUD.
- [ ] Draft/submitted/review/approve/reject states.
- [ ] Adjustment journal preview.
- [ ] Post approved adjustment.
- [ ] Reverse adjustment.
- [ ] Auto-reversing entry support.
- [ ] Attachments.
- [ ] Comments.
- [ ] Statement-line comments.
- [ ] P&L/Balance Sheet impact preview.
- [ ] Provisional, adjusted and final report versions.
- [ ] Prevent automatic “Audited” status.
- [ ] Permission and audit tests.
- [ ] Commit BS-09.

---

# BS-10 — Financial period and year close

- [ ] Closing checklist model.
- [ ] Mandatory/optional checklist items.
- [ ] Reconciliation gates.
- [ ] Pending posting gate.
- [ ] Trial Balance gate.
- [ ] Balance Sheet gate.
- [ ] Closing inventory snapshot.
- [ ] Report snapshot.
- [ ] Current-year result transfer.
- [ ] Next-year opening journals.
- [ ] Period/year lock.
- [ ] Duplicate-close prevention.
- [ ] Authorised reopen with reason/approval.
- [ ] Reopen impact on next-year opening.
- [ ] Full audit events.
- [ ] Close/reopen integration tests.
- [ ] Commit BS-10.

---

# BS-11 — Projection engine

## Scenario

- [ ] Scenario CRUD/clone/archive.
- [ ] Conservative/Base/Optimistic/Custom types.
- [ ] Actual baseline import.
- [ ] Monthly horizon 1–5 years.
- [ ] Assumption versioning.
- [ ] Approval state.

## Assumptions

- [ ] Revenue growth.
- [ ] Seasonality.
- [ ] New store.
- [ ] Average bill/customer count.
- [ ] Returns/discount.
- [ ] Gross margin.
- [ ] Purchase inflation.
- [ ] Inventory days.
- [ ] Debtor days.
- [ ] Creditor days.
- [ ] Employee/salary.
- [ ] Rent/expense escalation.
- [ ] Capex/depreciation.
- [ ] Debt/interest/repayment.
- [ ] Capital/drawings.
- [ ] Tax/minimum cash.

## Outputs

- [ ] Projected P&L.
- [ ] Projected Balance Sheet.
- [ ] Projected Cash Flow.
- [ ] Debt schedule.
- [ ] Working-capital requirement.
- [ ] Break-even.
- [ ] Ratios.
- [ ] Scenario comparison.
- [ ] Balance validation each month.
- [ ] No unexplained balancing figure.
- [ ] Excel/PDF export.
- [ ] Golden/model tests.
- [ ] Commit BS-11.

---

# BS-12 — TallyPrime exchange and CA package

## Tally mapping/export

- [ ] Tally profile/release metadata.
- [ ] Account group mapping.
- [ ] Ledger mapping.
- [ ] Voucher type mapping.
- [ ] Tax mapping.
- [ ] Stock/cost-centre mapping.
- [ ] Excel preview.
- [ ] Masters export.
- [ ] Transactions export.
- [ ] Control totals.
- [ ] Duplicate policy.
- [ ] Exceptions.
- [ ] XML fixture.
- [ ] JSON fixture.
- [ ] Validate against approved Tally test company when available.
- [ ] No direct production Tally posting.

## CA package

- [ ] Trial Balance.
- [ ] General Ledger.
- [ ] P&L.
- [ ] Balance Sheet.
- [ ] Cash Flow.
- [ ] Debtors/creditors.
- [ ] Inventory.
- [ ] Fixed assets.
- [ ] Bank reconciliation.
- [ ] GST/TDS.
- [ ] Payroll.
- [ ] Adjustments.
- [ ] Ratios.
- [ ] Source control totals.
- [ ] Exception report.
- [ ] Supporting documents.
- [ ] README.
- [ ] ZIP checksum and audit record.
- [ ] Commit BS-12.

---

# BS-13 — Security, audit and documentation

- [ ] Complete permission matrix.
- [ ] No default access for biller/POS roles.
- [ ] Tenant-isolation tests.
- [ ] Cross-company access tests.
- [ ] ID enumeration tests.
- [ ] Audit event coverage.
- [ ] Account mapping change audit.
- [ ] Journal/reversal audit.
- [ ] Close/reopen audit.
- [ ] Export/package audit.
- [ ] Attachment validation.
- [ ] Accountant user guide.
- [ ] CA review guide.
- [ ] Developer architecture guide.
- [ ] Posting-rule guide.
- [ ] Backfill runbook.
- [ ] Reconciliation runbook.
- [ ] Closing runbook.
- [ ] Rollback runbook.
- [ ] Commit BS-13.

---

# BS-14 — Full QA and hardening

- [ ] Backend clean build.
- [ ] Backend full tests.
- [ ] Frontend typecheck.
- [ ] Frontend lint.
- [ ] Frontend production build.
- [ ] Existing regression suite.
- [ ] Clean DB migration.
- [ ] Existing DB migration.
- [ ] Additive migration diff reviewed.
- [ ] Large ledger pagination test.
- [ ] Backfill batch/resume test.
- [ ] Large export streaming test.
- [ ] Concurrent posting test.
- [ ] Feature-disabled regression.
- [ ] Browser route QA.
- [ ] Permission QA.
- [ ] Accessibility/basic keyboard QA.
- [ ] No secrets.
- [ ] No production host changes.
- [ ] No deployment triggered.
- [ ] Known limitations documented.
- [ ] Commit BS-14.

---

# BS-15 — Merge-readiness package

- [ ] Rebase/update branch from latest `version6` using approved policy.
- [ ] Resolve conflicts.
- [ ] Rerun full validation.
- [ ] Prepare commit list.
- [ ] Prepare file/change summary.
- [ ] Prepare migration summary.
- [ ] Prepare feature-flag activation steps.
- [ ] Prepare staging-only backfill plan.
- [ ] Prepare reconciliation evidence.
- [ ] Prepare test evidence.
- [ ] Prepare production rollout proposal.
- [ ] Prepare rollback proposal.
- [ ] Confirm no auto-merge.
- [ ] Confirm no auto-deploy.
- [ ] Request human review.
- [ ] Do not merge.

Final evidence:

```text
Branch head:
Base version6 commit:
Commits:
Backend build:
Backend tests:
Frontend typecheck:
Frontend build:
Migration review:
Reconciliation dataset:
Trial Balance result:
Balance Sheet result:
Projection model result:
Known limitations:
Reviewer:
Decision:
```

---

# Permanent invariants checklist

These must stay checked after every stage:

- [x] Active branch is `balancesheet`.
- [x] Module feature flag defaults to disabled.
- [x] No production deployment.
- [x] No production migration.
- [x] No operational table drop/rename.
- [x] No posted journal edit/delete.
- [x] No duplicate source posting.
- [x] Tenant/company/store isolation is preserved.
- [x] Existing app builds with module disabled.
- [x] TODO evidence is current.

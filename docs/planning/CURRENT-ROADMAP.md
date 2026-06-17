## Stage 8H Package 3 Purchase and Voucher Crash Hotfix / v4.7.2

- Fixed empty SelectItem crashes in Debit Note, Credit Note and New Purchase inward selectors by replacing empty-string option values with safe sentinels.
- Hardened Cash Voucher refresh so list loading is not blocked by optional lookup/default repair failures.
- Changed Vendor Payments add flow back to the standard sliding dialog used by other money-entry pages.
- Version: 4.7.2
- Build code: `GARMETIX-8H-20260617-4720`

## Stage 8H Package 2 Access HR Payroll Purchase Stabilization / v4.7.1

- Legacy Overview access hardened for admin/owner only, including direct `/` navigation and legacy shell menu filtering.
- Payroll policy now supports store manager and remote accountant access for payslip/salary payment visibility.
- Salary Structures tab remains restricted to admin/owner/accountant/remote-accountant/payroll roles.
- HR Attendance Add Attendance action verified/restored.
- Purchase inward full page and vendor payment page/list verified.
- Version: 4.7.1
- Build code: `GARMETIX-8H-20260617-4710`

## Stage 8H Package 1 Post-Go-Live Acceptance Stabilization / v4.7.0

- Added post-go-live acceptance center for the recent role/menu, HR/payroll, attendance, purchase inward and vendor-payment fixes.
- Added Mac mini post-go-live acceptance script for health/readiness/smoke endpoints and manual role-check reminders.
- Preserved Stage 8G final go-live baseline and npm registry hotfix.
- Version: 4.7.0
- Build code: `GARMETIX-8H-20260617-4700`

## Stage 8G Package 9 Final Go-Live Acceptance / v4.6.8

- Added Stage 8G Completion admin page for final production sign-off.
- Added final Mac mini acceptance script covering health, app-info, production readiness, backup maintenance and runtime smoke endpoints.
- Added role acceptance matrix script and documentation for Admin/Owner, Store Manager, Accountant, Power User and Normal User verification.
- Added final production release checklist documentation.
- Fixed runtime smoke version validation so it tracks the active Stage 8G build instead of stale v4.6.0 constants.
- Stage 8G is now complete pending live-site sign-off on the Mac mini.
- Version: 4.6.8
- Build code: `GARMETIX-8G-20260617-4680`

## Stage 8G Package 8 Access HR Payroll Purchase Stabilization / v4.6.7

- Restricted Legacy Overview to admin/owner only.
- Corrected HR/Payroll role visibility: payroll visible to store manager/accounting/power users while Salary Structures remains restricted.
- Restored explicit Attendance add action.
- Added full-page purchase inward entry at `/purchase/new`.
- Added vendor payment page/register for invoice-linked and advance payments.
- Improved production password-reset feedback when SMTP is not configured.
- Version: 4.6.7
- Build code: `GARMETIX-8G-20260617-4670`

## Stage 8G Package 7 Production Security and Log Retention Hardening / v4.6.6

- Added Mac mini production security hardening checks for HTTPS, secrets, CORS, localhost-only ports, Docker auto-start and public security headers.
- Added Docker log-retention verification helper for API, web, Postgres and Cloudflare tunnel containers.
- Hardened production preflight with CRLF-safe env loading, reset-database protection and log-retention checks.
- Version: 4.6.6
- Build code: `GARMETIX-8G-20260617-4660`

## Stage 8G Package 6 Oracle Cloud Sync Validation / v4.6.5

- Added Oracle Cloud wallet/TNS readiness script for Mac mini deployments.
- Added go-live documentation for Oracle connection test, external-app event review/apply, auto-apply policy and dead-letter monitoring.
- Added Oracle sync environment placeholders to production env examples.
- Version: 4.6.5
- Build code: `GARMETIX-8G-20260617-4650`

## Stage 8G Package 5 GSTIN Provider Validation / v4.6.4

- Added GSTIN provider status and admin test endpoints.
- Added Production Readiness GSTIN provider validation panel.
- Added Mac mini GSTIN readiness script and provider configuration documentation.
- Version: 4.6.4
- Build code: `GARMETIX-8G-20260617-4640`

## Stage 8G Package 4 Google Drive Backup Validation / v4.6.3

- Backup Maintenance now includes Google Drive off-site status, service-account identity, folder id, cloud backup count and upload action.
- Added Mac mini Google Drive configuration check and latest-backup upload preparation scripts.
- Fixed Google Drive download service duplicate local variable risk.
- Version: 4.6.3
- Build code: `GARMETIX-8G-20260617-4630`

## Stage 8G Package 3 SMTP Email Delivery Validation / v4.6.2

- Added admin-only SMTP diagnostics endpoints.
- Added Production Readiness UI to review masked SMTP configuration and send a real test email.
- Added operation notes for configuring SMTP in `.env.production`.
- Version: 4.6.2
- Build code: `GARMETIX-8G-20260617-4620`

## Stage 8G Package 2 Go-Live Readiness and Branding Restore / v4.6.1

- Restored the earlier provided Garmetix/Aadwika Fashion logo in the login/title experience and sidebar brand assets.
- Replaced placeholder/generated app icon assets with the earlier provided branding set.
- Added go-live readiness and backup-restore drill helper scripts for Mac mini Linux/WSL operations.
- Version: 4.6.1
- Build code: `GARMETIX-8G-20260617-4610`

## Stage 8G Package 1 Backup Restore Maintenance / v4.6.0

- Backup Maintenance Center added under Maintenance.
- Local backup health now checks disk space, write access, recent backup age, checksum coverage, manifest coverage, orphan sidecars and restore temp files.
- Admins can create backups, verify all backups and run safe cleanup from the UI.
- Mac mini Linux/WSL helper scripts added for backup maintenance and direct PostgreSQL backups.
- Version: 4.6.0
- Build code: `GARMETIX-8G-20260617-4600`

## Stage 8F Package 2 Automated Smoke Tests / v4.5.1

- Added test-automation manifest and runtime smoke endpoints for deployment validation.
- Added backend xUnit catalog contract tests covering backend, frontend, Docker and authenticated API smoke definitions.
- Added Linux/WSL and Windows automated test runners for backend tests, Nuxt builds, optional frontend smoke and Docker health checks.
- Added browserless frontend smoke script that checks the public login page, app-info version and test-automation manifest.
- Hardened smoke scripts for CRLF-safe env loading and optional authenticated release/readiness checks.
- Version: 4.5.1
- Build code: `GARMETIX-8F-20260617-4510`

## Stage 8F Package 1 Audit History Foundation / v4.5.0

- Persistent AuditLogEntries capture create/update/delete from DbContext SaveChanges.
- Audit events preserve actor, route, trace id, before/after snapshots and changed-field list.
- Sensitive password/token/secret/API-key fields are excluded from snapshots.
- Existing audit screen now reads real events first with legacy fallback for older rows.
- Version: 4.5.0
- Build code: `GARMETIX-8F-20260617-4500`

# Garmetix Current Roadmap

Updated: 2026-06-17


## Stage 8E Package 7 Tailoring and Alteration Workflow / v4.4.4

- [x] Added tailoring/stitching orders with SOP: order -> delivery -> service invoice -> payment.
- [x] Added readymade garment alteration workflow linked to tailoring vendors and original product/invoice context.
- [x] Added reusable tailoring service items for stitching and alteration services.
- [x] Added customer receipt, vendor payment, delivery schedule, pending/completed status and order history tracking.
- [x] Added in-house alteration expense/profit impact tracking so product margin is reduced when alteration cost is absorbed by the store.
- Version: 4.4.4
- Release: Tailoring and Alteration Workflow
- Build code: `GARMETIX-8E-20260617-4440`

## Stage 8E Package 6 Due and Payment Dashboards / v4.4.3

- Added business-dashboard customer due rows with bill count, billed amount, paid amount, due amount and age bucket.
- Added vendor due rows calculated from purchase invoices less recorded purchase payments.
- Added cash and payment summary across sales collections, purchase payments and accounting vouchers by payment mode.
- Added store-group comparison view combining sales, purchase, customer due, vendor due, cash in/out, net cash and stock value.
- Export actions now include customer dues, vendor dues, payment-mode summary and store-group cash/due comparison.

## Stage 8E Package 5 Ledger Synchronization Hardening / v4.4.2

- Added ledger synchronization health check and repair endpoints for party and bank-account ledger links.
- Party ledgers are now protected against missing ledgers, wrong-company links, non-party ledgers, wrong ledger types, and shared ledger links.
- Bank-account ledgers are now protected against missing ledgers, wrong-company links, wrong ledger types, shared ledger links, and ledger-name drift.
- Added Accounting → Ledger Sync tab with issue counts, severity, fix action, and one-click repair.
- Hardened party and bank-account save flows so new/edited masters relink only to safe, reusable ledgers or create a dedicated ledger.

## Stage 8E Package 4 Financial Year Locking and Journal Validation / v4.4.1

- Added Financial Year Locks API and workspace to close company/store periods safely.
- Locked periods now block back-dated sales, purchase, inventory, GST and accounting changes at DbContext save time.
- Added journal validation endpoint and UI summary for unbalanced entries, missing lines, negative amounts and mixed debit/credit lines.
- Added journal-line save validation so new postings cannot persist invalid debit/credit line shapes.
- Changed Mac mini deployment defaults so `RESET_DATABASE_ON_DEPLOY=false` is the safe default in `deploy/macmini.env`.

## Stage 8E Package 3 Bank Reconciliation and Cheque Lifecycle / v4.4.0

- Added bank reconciliation summary endpoint with open/reconciled debit-credit totals and statement-line reconciliation metadata.
- Added statement-line reconcile and reopen actions with operator, timestamp, reference, remarks, and linked transaction preservation.
- Added cheque lifecycle actions for Issued, Deposited, Cleared, Bounced, and Cancelled states with action timestamps and lifecycle remarks.
- Added Accounting UI Bank Reconciliation tab, open-line metric, reconcile/reopen actions, and quick cheque clear/bounce actions.
- Kept clean fresh-install schema baseline and WSL Mac mini Cloudflare deployment defaults.

## Stage 8E Package 2 Clean Initial Migration / v4.3.9

- Removed historical EF Core incremental migrations from the deploy package.
- Added a single baseline migration marker: `20260617000000_InitialFreshSchema`.
- Docker production defaults to `Database:SchemaBootstrapMode=FreshBaseline`, creating the schema from the current `GarmetixDbContext` model.
- Added one-time PostgreSQL volume reset support for clean/fresh server installs.

## Current Baseline

- Previous version: 4.4.3
- Stage: Stage 8E Package 6
- Release: Due and Payment Dashboards
- Build code: `GARMETIX-8E-20260617-4430`
- Branch: `Version3.0`
- Pre-Stage 8 baseline commit: `470ba2e`

Stage 7M menu names, route compatibility, permission-aware navigation, Off Book separation, and the legacy-shell rollback option are part of the baseline and must be preserved.

## Completed Before Stage 8 - Operations Priority Patch

- [x] Petty cash previous-day carry-forward, transaction pre-calculation, editable verification, mismatch logging/email, latest cash widget, and automatic A5 two-section printing.
- [x] Server-owned voucher numbering using store code, year-month, and one monthly numeric sequence.
- [x] Revised compact voucher print defaults and document labels.
- [x] Shared frontend GET cache and in-flight request de-duplication to reduce repeated page-load work.



## Stage 8E Package 2 Deployment Runtime Hotfix / v4.3.9

- [x] Hardened `20260614094500_SeparateNonGstGoodsFromBooks` for databases where `NonGstGoodsDocuments` is absent during migration replay.
- [x] Prevented migration crash `42P01: relation "NonGstGoodsDocuments" does not exist` during Docker API startup.
- [x] Added Nuxt Icon local server bundle for `lucide` icons to reduce runtime dependency on `api.iconify.design`.

## Stage 8E Package 2 Compile Hotfix / v4.3.9 deploy package

- [x] Fix backend publish failure in `AccountingPostingService.UpsertManualChequeLogAsync` caused by an out-of-scope `paymentReference` variable.
- [x] Preserve invoice cheque payment reference handling in `UpsertInvoiceChequeLogAsync` while using the bank transaction reference for manual cheque logs.

## Stage 8E Package 2 Hotfix 1 / v4.3.9 - Sales Return FK Stability

- [x] Fix sales return save failure caused by blank `SalesInvoices.SalemanId` on generated return invoices.
- [x] Copy the original invoice salesman to return and exchange invoices, with active store fallback when old data has a missing/stale reference.
- [x] Prevent regular billing and billing import from creating invoices with `Guid.Empty` salesman references.
- [x] Replace billing import payment-details JSON string interpolation with serializer-based JSON to keep the code compile-safe.

## Stage 8D Priority Patch / v4.3.5 - Sales Invoice Stability

- [x] Replace the New Invoice dialog with a dedicated `/billing/new` workspace.
- [x] Preserve incomplete invoice work in local draft storage and reset only after a successful save.
- [x] Search customers by mobile without preloading the full customer master; automatically create unmatched customers when the invoice is saved.
- [x] Keep GSTIN visible, preserve GSTIN checking and customer balances, and default the salesman to Manager.
- [x] Support amount or percent line discounts and show the requested desktop/mobile item columns.
- [x] Round the final bill and payment target to whole rupees and show the round-off amount.
- [x] Hide customer adjustments by default and show payment fields according to the selected payment mode.
- [x] Automatically print a newly saved invoice and keep the page ready for the next sale.
- [x] Run sale create, cancel, return and exchange transactions through the configured PostgreSQL retry execution strategy.
- [x] Determine B2B interstate supply from company/customer GSTIN state codes and split GST as IGST when required.
- [x] Correct customer-advance adjustment precedence so an advance receipt is not consumed as generic store credit.
- [x] Verify Docker runtime, desktop layout, 390px mobile layout and no horizontal overflow.

## Priority 0 - Baseline Stabilization

- [x] Fix `GET /api/dashboard/home` returning an empty response body.
- [x] Resolve the nullable warnings in purchase receipt and data-consistency code.
- [x] Keep frontend, backend, package, release, and build-code versions synchronized.
- [x] Remove build-time dependence on external font metadata by pinning Nuxt font resolution to local/no-provider mode.
- [x] Add one current aggregate validation command instead of relying on version-specific historical scripts.
- [ ] Verify login, smart-dashboard routing, workspace selection, role navigation, and API health in Docker.

## Stage 8A / v4.0 - Full UI Audit and Standardization

- [x] Package 1: make `/ui-audit` persistent/actionable and standardize the Credit Note and Debit Note registers with shared loading, error, empty, search, action, and responsive behavior.
- [x] Package 2: standardize Commercial Notes and Customers, restore hidden primary actions, and synchronize all runtime version sources to v4.0.0.
- [x] Package 3: standardize Parties and Vouchers, add retained sanitized errors, expose both party create actions, add voucher-type filtering, and migrate audit progress to v4.0.1.
- [x] Package 4: standardize Loyalty and Petty Cash, widen daily-cash entry, preserve transaction calculation/A5 printing, and migrate audit progress to v4.0.2.
- [x] Package 5: standardize Billing and Sales Return, preserve the wide invoice workspace, and require bank linkage for non-cash return refunds in v4.0.3.
- [x] Package 6: fix local-date preservation in Petty Cash/Purchase Return and standardize Purchase and Purchase Return in v4.0.4.
- [x] Package 7: standardize Inventory/Stock Operations, add universal document QR lookup, guarded factory reset, and server PDF print hardening in v4.0.5.
- [x] Package 8: repair salary-payment saving, add advance/due/already-paid pre-calculation, whole-rupee payment rounding, and server-owned `StoreCode/YYYYMM/SPAY/series` numbering in v4.0.6.
- [x] Package 9: harden Accounting and HR with server-owned Party/Bank ledger linkage, wide accounting forms, retained register errors, hidden internal party linkage, and timezone-neutral local dates in v4.0.7.
- [x] Package 10: standardize Reports, GST Reports, Import/Export, Audit Trail, and Message Logs with retryable register states, responsive data surfaces, and local-date-safe report defaults in v4.0.8.
- [x] Package 11: repair Product Master retryable transactions and persist API failures/write events plus frontend messages and unhandled errors in Message Logs in v4.0.9.
- [x] Package 12: standardize Cash Vouchers with retained retry errors, type filtering, wide entry, local-date preservation, and explicit Off Book separation in v4.0.10.
- [x] Package 13: separate Non-GST sale, purchase, stock, settlement, PDF and QR workflows from regular accounting, GST, billing, purchase and inventory in v4.0.11.
- [x] Package 14: standardize Company Setup, Client Onboarding, AF/SS Defaults, and Roles & Users with retryable states, wide forms, responsive tables, local dates, and business-facing copy in v4.0.12.
- [x] Package 15: standardize Production Readiness, Release Stabilization, Data Consistency, and Oracle Sync with retained retry errors, loading states, responsive data surfaces, and safe select values in v4.0.13.
- [x] Package 16: complete GST Returns, Profile, About Garmetix, Contact Us, and Help and FAQ; close the Stage 8A page queue with local-date-safe GST preparation and retryable help/profile states in v4.0.14.
- [x] Complete every page in the `/ui-audit` queue.
- [x] Standardize page headers, filters, tables, actions, loading, error, and empty states.
- [x] Move large invoice, voucher, employee, purchase, payroll, and other master-detail forms to full pages or wide workspaces.
- [x] Check button wrapping, table overflow, modal spacing, and sidebar/topbar/footer overlap at mobile, tablet, and desktop sizes.
- [x] Replace repeated custom markup with shared Nuxt UI components where it reduces real duplication.
- [x] Remove stale visible implementation or migration text from business-facing pages.

## Stage 8B / v4.1 - Access and User Administration Hardening

- [x] Verify permission-aware mobile sidebar, collapsed icon mode, active-menu state, command palette, footer menus, and topbar actions in v4.1.2.
- [x] Implement a permission-aware notification/action dropdown without exposing technical noise in v4.1.2.
- [x] Audit and harden the existing user create/edit, role assignment, password reset, and active/inactive workflows in v4.1.0.
- [x] Centralize and test the server/frontend permission matrix in v4.1.1.
- [x] Confirm StoreManager can view and enter permitted store records but cannot access Admin or edit/delete restricted records.
- [x] Confirm edit access is limited to Owner, Admin, PowerUser, and Accountant, and delete access to Owner/Admin.
- [x] Add explicit audit events for user creation/editing, role assignment, activation, deactivation, password administration, and deletion in v4.1.0.
- [x] Add automated access tests for Owner, Admin, PowerUser, Accountant, StoreManager, Salesman, HR, and Payroll roles in v4.1.1.

## Stage 8C - Purchase Return and Vendor Settlement

- [x] Add formal Purchase Return and Purchase Return Item records in v4.2.0.
- [x] Preserve product, HSN, GST, quantity, rate, reason, vendor, and original-purchase snapshots in v4.2.0.
- [x] Generate linked debit notes and colored A4/A5 purchase-return PDFs with complete items, GST reversal totals, QR lookup, automatic first print, and print audit state in v4.2.1.
- [x] Add vendor refund, adjustment, and outstanding settlement workflows in v4.2.2.
- [x] Implement exact item-level input-tax-credit reversal in v4.2.3.
- [x] Preserve and visibly reconcile links among purchase, return, stock movement, debit note, ledger, settlement, and audit history in v4.2.3.

## Stage 8D - Inventory Documents and Valuation

- [x] Add formal Stock Adjustment, Transfer, and Physical Count header/detail documents in v4.3.0.
- [x] Make the stock movement ledger the authoritative stock source in v4.3.1.
- [x] Introduce and document weighted-average valuation in v4.3.1; evaluate FIFO only after that baseline is stable.
- [x] Post shortage, excess, write-off, and transfer accounting entries in v4.3.2.
- [x] Add stock ageing, configurable low-stock risk, weighted-average valuation, and stock-reconciliation reports in v4.3.3.
- [x] Add sequence and stock-concurrency tests, including negative-stock policy, in v4.3.4.

## Stage 8E - Accounting and Payment Hardening

- [x] Add Owner/Admin-only Off Book Cash Voucher conversion for eligible cash accounting vouchers in v4.3.6.
- [x] Preserve immutable conversion reason, direction, operator, timestamp, amount, and source/destination document snapshots in v4.3.6.
- [x] Remove regular accounting postings when moving a cash voucher Off Book and retain the source as an audit-only record in v4.3.6.
- [x] Post separate ledger lines for each split payment row in v4.3.9.
- [x] Persist structured payment detail snapshots for card, UPI, bank, cheque, account, reference, gateway, settlement and adjustment rows in v4.3.9.
- [x] Prevent duplicate application of advances, credit notes, loyalty value, and store credit in v4.3.9.
- [x] Complete bank reconciliation and cheque issue/deposit/clear/bounce lifecycle auditing in v4.4.0.
- [x] Add financial-year locking and stronger journal-balancing checks in v4.4.1.
- [x] Harden automatic Party and Bank Account ledger synchronization in v4.4.2.
- [x] Add customer/vendor due dashboards, cash/payment summaries, and store-group comparison views in v4.4.3.

## Stage 8F - Audit and Automated Tests

- [ ] Add before/after JSON audit history for important entities.
- [ ] Add xUnit unit and API integration tests.
- [ ] Add PostgreSQL transaction, sequence, and concurrency tests.
- [ ] Add accounting, GST, payroll, purchase-return, and stock reconciliation tests.
- [ ] Add Playwright coverage for critical Nuxt workflows.
- [ ] Test backup and restore using a disposable database.

## Stage 8G - External Integration and Go-Live

- [ ] Validate real SMTP delivery.
- [ ] Validate Google Drive backup and restore using production credentials.
- [ ] Configure and validate the selected licensed GSTIN provider.
- [ ] Test Oracle Cloud wallet/TNS, external-app event flow, review/apply, and final auto-apply policy.
- [ ] Run clean Docker installation and fresh-database migration drills.
- [ ] Complete HTTPS, secrets, scheduled backups, retention, and log rotation.
- [ ] Run the complete role acceptance matrix and production release checklist.

## Dashboard Analytics Carried Forward from the Historical Extra List

The following were explicitly requested in the older extra list and remain acceptance items where not already complete:

- stock ageing and low-stock risk,
- customer and vendor due views,
- cash and payment summaries,
- store-group comparison charts,
- mobile navigation and collapsed-sidebar verification,
- notification/action UX,
- loading, retry, and empty-state consistency,
- documented shell revert and version history.

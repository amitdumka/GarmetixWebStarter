# Garmetix Master TODO

Current implementation order is maintained in `CURRENT-ROADMAP.md`. This file preserves the complete requested-feature checklist and implementation history.

Use this file as the running handoff checklist. When a task is completed, mark it with `[x]` and add a short note/date if useful.

## Priority Operations Patch - 2026-06-12

- [x] Carry each petty cash sheet opening balance from the previous day's closing cash in hand.
- [x] Pre-calculate petty cash from daily invoice, voucher, and bank-cash activity while keeping values editable for verification.
- [x] Compare entered petty-cash values with transaction-derived values and write owner-visible warning details to Message Logs.
- [x] Email Owner/Admin users for petty-cash mismatches when SMTP delivery is enabled; email failure does not block saving.
- [x] Open an A5 landscape, two-section Income/Payment print layout after petty-cash save.
- [x] Show latest working-store cash in hand instead of summing historical closing balances.
- [x] Generate voucher numbers server-side as `StoreCode/yyyyMM/0001`, using one monthly number series per store.
- [x] Revise voucher print defaults and accounting-document labels.
- [x] Add short-lived API GET caching, longer master-data caching, in-flight request de-duplication, and write invalidation.

## Payroll Payment Patch - 2026-06-13

- [x] Replace direct SalaryPayment entity binding with a dedicated save request to prevent malformed navigation/identifier payloads.
- [x] Pre-calculate base deductions, current-month salary advance, previous company due, already-paid amount, outstanding amount, and round-off.
- [x] Include salary advance in total deductions and add previous company due to net payable.
- [x] Keep calculated salary figures editable and round the final paid amount to whole rupees.
- [x] Apply payroll round-off to due and carry-forward calculations so paise-only balances do not remain open.
- [x] Reject duplicate or over-limit salary payments after the rounded outstanding salary is settled.
- [x] Generate salary-payment vouchers server-side as `StoreCode/YYYYMM/SPAY/0001` using a monthly store sequence.
- [x] Preserve local salary-payment dates without UTC previous-day conversion.
- [x] Exclude deleted salary payments and advances from payroll due calculations.

## Accounting and HR Hardening - 2026-06-14

- [x] Replace Party and Bank Account entity binding with dedicated save requests that exclude internal ledger and navigation fields.
- [x] Keep Party `LedgerId`, Ledger `IsParty`, and Bank Account `LedgerId` entirely server-owned.
- [x] Show only the contra ledger for bank transactions and resolve linked Party records internally.
- [x] Move accounting create/edit forms from a narrow drawer to responsive wide modal workspaces.
- [x] Add retained retryable loading/error/empty states to Accounting and HR registers.
- [x] Preserve selected HR attendance, employee, bank, cheque, ledger and account dates without UTC previous-day conversion.
- [x] Replace the HR empty-value store option with an explicit all-stores sentinel.

## Cash Voucher Conversion Audit - 2026-06-16

- [x] Restrict Off Book/on-book cash voucher conversion to Owner and Admin.
- [x] Allow only cash payment, receipt, and expense accounting vouchers to move Off Book.
- [x] Remove journal, bank statement, bank transaction, and cheque postings when a regular voucher moves Off Book.
- [x] Keep converted source records as hidden audit rows instead of duplicating or destroying their history.
- [x] Store immutable conversion direction, document numbers, amount, party, reason, operator, and timestamp.
- [x] Block edit/delete of converted documents and show conversion history in the Cash Voucher workspace.

## Reports and Data Operations UI Hardening - 2026-06-14

- [x] Standardize Reports Center, GST Reports, Import/Export, Audit Trail, and Message Logs with shared retryable register states.
- [x] Preserve local Indian calendar dates in the Reports Center default date range.
- [x] Keep report and administrative load errors visible until a successful retry.
- [x] Keep large reports and audit tables inside responsive, scrollable containers.
- [x] Replace oversized Message Log panels with compact operational layouts and expandable technical details.
- [x] Replace empty Message Log select values with explicit all-value sentinels so SSR does not fail.
- [x] Pass the active workspace company to GST report and CSV requests.
- [x] Keep API paths out of retained business-facing register errors while preserving details in browser logs.
- [x] Mark all five Package 10 routes reviewed in the persistent UI audit queue.

## Auth / Login

- [x] Remove "Admin is available" message.
- [x] Add Forgot Password from login screen.
- [x] Add Reset Password from login screen.
- [x] Add Change Password in current user profile.
- [x] Add production email delivery for forgot-password reset link/token. SMTP sender added; SMS still optional for future.
- [x] Add optional reset-token persistence/revocation table for stricter production security. Added `PasswordResetTokens` table, token hashing, one-time use, and revocation on new reset/change password.

## Company / Store / StoreGroup Permission Flow

- [x] Show Company, StoreGroup, and Store based on logged-in user permissions only. Added `/api/workspace/options` and server-side scope filtering.
- [x] Auto-select default company/store after login. Workspace selector now initializes from allowed/default backend options.
- [x] Add clean topbar flow to change company/store group/store. Topbar/mobile workspace modal uses allowed scoped options and locks assigned scopes.
- [x] Block module transactions when no valid working store is selected. Billing, purchase, and quick-product validate the selected store against server-side user scope.

## Backup / Restore

- [x] Add guarded factory reset with typed confirmation, automatic safety backup, migration preservation, and current-admin preservation.
- [x] Local backup and restore.
- [x] Google Drive credential setup. Added service-account JSON configuration and Docker secret mount.
- [x] Upload backups to Google Drive. Local manual/scheduled backups can upload automatically or manually from System Health.
- [x] Restore/download backups from Google Drive. Added cloud list/download/delete/restore endpoints and UI actions.
- [x] Show online backup status and schedule history. System Health now shows Google Drive status, cloud files, last error/action, and online backup count.

## Billing / Invoice Printing

- [x] Print a stable QR code on every invoice, purchase document, voucher, cash voucher, debit/credit note, petty-cash sheet, payslip, and salary-payment slip.
- [x] Add a document scanner/search page that resolves QR and barcode values to the permitted entry page.
- [x] Replace dashboard-DOM printing with authenticated server PDF printing and validate generated PDF page sizes/content.
- [x] Trigger print only after newly creating printable vouchers/documents; editing must not auto-print.
- [x] Add industry-standard printable payslip and salary-payment slip PDFs.
- [x] Stage 8A Package 5 UI pass: shared retryable Billing/Sales Return registers, invoice status filtering, wide entry workspaces, and bank-linked non-cash refunds.
- [x] Standard A4 invoice print layout. Added print/PDF format `a4` from Billing receipt modal.
- [x] Standard A5 invoice print layout. Added print/PDF format `a5` from Billing receipt modal.
- [x] Thermal 2-inch receipt layout. Added print/PDF format `thermal-2` for 58mm receipts.
- [x] Thermal 3-inch receipt layout. Added print/PDF format `thermal-3` for 80mm receipts.
- [x] Download PDF invoice. Added `GET /api/billing/sales/{id}/pdf` and Billing page download button.
- [x] Reprint/copy options for invoices. Added customer/office/duplicate copy labels plus reprint/signature toggles.


## GSTIN Verification - URGENT

- [x] Add GSTIN lookup/verification for Customer and Vendor creation. Added configurable provider service and `/api/gstin` endpoints.
- [x] Fetch and store GSTIN details on relevant party model. Customer/Vendor now store legal name, trade name, principal address, state code, taxpayer type, status, verification time/source, and mismatch alert.
- [x] Alert when entered party name/address does not match GSTIN data. Added backend validation alerts plus Party, Billing, and Purchase UI warnings.
- [x] Connect to final licensed GSTIN provider credentials in production `.env`. Config keys and docs are in place; add real provider values during deployment.

## GST Returns - URGENT

- [x] Create standalone GST Return module shell without linking Billing/Purchase. Added `/gst-returns` frontend page and `/api/gst-returns` backend group.
- [x] Generate GSTR-1 JSON from manual/separate entry data.
- [x] Generate GSTR-1 Excel from manual/separate entry data.
- [x] Generate GSTR-3B JSON from manual/separate entry data.
- [x] Generate GSTR-3B Excel from manual/separate entry data.
- [x] Review generated JSON/Excel against latest GST portal/offline utility templates before production filing. Added schema-review endpoint, Excel checklist, stronger GSTIN/POS/rate/tax validations, and portal-validation warnings.
- [x] Add saved GST return drafts and audit trail. Added `GstReturnDrafts` and `GstReturnAuditEntries`, save/load/delete/mark-filed flows, draft export, and audit panel.
- [x] Link GST module with Billing/Purchase after manual module approval. Added Load From Books endpoints/UI for GSTR-1 and GSTR-3B while keeping manual draft editing available.

## Reports

- [x] PDF export for reports. Added PDF/Save-as-PDF action through report print layout.
- [x] Excel export for reports. Added Excel `.xls` export from current report rows.
- [x] Print reports. Existing report print action now also caches the snapshot before printing.
- [x] Cache repeated/same report results. Added local report snapshot cache/load/live controls keyed by report/filter/search.

## Purchase

- [x] Stage 8A Package 6 UI pass: shared retryable Purchase/Purchase Return registers, status filtering, wide return workspace, and timezone-neutral local date handling.
- [x] Purchase invoice list and detail/print view. Added `/api/purchase/invoices/recent`, receipt/PDF endpoints, and Purchase page view/print/download modal.
- [x] Purchase return/cancellation with stock reversal. Added cancel endpoint that reverses inward stock and purchase accounting posting.
- [x] Vendor payment voucher creation from purchase payment. Added purchase invoice payment-voucher endpoint and Purchase page Pay action that creates regular vendor payment vouchers with accounting posting.
- [x] Better tax/category selector in purchase inward form. Added tax/category/subcategory lookup and selectors for new inward products.

## Sales Return / Exchange

- [x] Partial sales return. Added selected-item sales return endpoint and Billing page return workflow.
- [x] Exchange item flow. Added exchange endpoint and Billing page flow: return selected items, create replacement invoice, apply store credit, and collect additional payment.
- [x] Return voucher/credit note. Sales returns now create a return invoice/credit note linked to the original invoice.
- [x] Stock reversal for selected returned items only. Return quantities reduce sold stock for only selected invoice items.
- [x] Customer balance adjustment. Return value reduces customer billed amount and creates store credit when refund is not paid immediately.

## Import / Export

- [x] CSV import UI. Existing Import Export page supports CSV upload, preview, validation, and commit flow.
- [x] Export UI. Existing Import Export page supports module export and template download.
- [x] Validation preview before import. Existing import flow supports validate-only mode with row preview and errors.
- [x] Error report download. Added CSV error report download from validation/import errors.
- [x] Admin import/export endpoint polish. Existing admin-protected endpoints expose module metadata, templates, exports, validate-only import, and committed import.

## Audit UI / Activity Logs

- [x] Filter audit by module/user/date/action. Audit page now has module/action/actor/entity/date/keyword filters and backend query support.
- [x] View changed fields. Audit page now opens entity field details and timestamp/change summary from backend audit detail endpoint.
- [x] Export audit report. Audit page now exports filtered audit rows to CSV.

## Testing / Deployment Hardening

- [x] Run full backend build on developer machine. Stage 7M Release build succeeds on 2026-06-11 with three warnings tracked in the current roadmap.
- [x] Run full Nuxt build on developer machine. Stage 7M Nuxt production build succeeds on 2026-06-11 with external font-provider and dependency warnings tracked in the current roadmap.
- [ ] Test Docker Compose clean install.
- [ ] Test fresh database migration.
- [ ] Test backup/restore.
- [ ] Test permissions with Admin, Owner, Manager, Cashier, Inventory, HR, Payroll, Accountant users.


## Current validation note

- Backend and full Nuxt SSR builds pass locally for Stage 7M.
- Remaining release validation is clean Docker installation, fresh database migration, backup/restore drill, and the complete role-permission matrix.
- Use `docs/operations/validation/Developer-Validation-Checklist.md`.

## Commercial Notes / Advance / Loyalty / Barcode - URGENT

- [x] Add Debit Note and Credit Note module for customers/vendors/manual parties.
- [x] Create debit/credit notes automatically from sales returns and purchase returns when amount is not adjusted immediately.
- [x] Allow manual Debit/Credit Note entry for any party.
- [x] Print Debit/Credit Note on A4 and A5 slip/two-copy style PDF.
- [x] Add advance payment receipt from customer and store it as customer credit balance.
- [x] Introduce store-level loyalty program and implement automatic points on sales with proportional reversal on returns.
- [x] Replace sale/purchase product dropdown workflow with barcode scan + autocomplete product lookup.
- [x] Cache product lookup data locally in browser for better availability.
- [x] Barcode lookup returns Name, Available Qty, MRP, TaxRate, Unit, Category, SubCategory, and HSNCode.
- [x] Add invoice/voucher scan code on printable PDF and scan lookup endpoint to fetch invoice/voucher records.

## Menu / Runtime Bug Fixes

- [x] Add visible Sales Return menu and `/sales-return` page.
- [x] Add visible Purchase Return menu and `/purchase-return` page.
- [x] Repair missing runtime tables for GST drafts, commercial notes, customer advances, loyalty programs, and loyalty ledger when older PostgreSQL volumes report migrations as already applied.

## Customer / Notes / Loyalty UI Refinement

- [x] Split combined Debit/Credit Note entry into dedicated Debit Note and Credit Note menu pages.
- [x] Added dedicated Debit Note entry/edit form routes: `/debit-notes/new` and `/debit-notes/{id}`.
- [x] Added dedicated Credit Note entry/edit form routes: `/credit-notes/new` and `/credit-notes/{id}`.
- [x] Converted `/commercial-notes` to a summary/register page only, not a mixed entry form.
- [x] Added dedicated Customer module `/customers` with list, metrics, GST status, credit balance, and loyalty balance.
- [x] Added dedicated customer create/edit form routes: `/customers/new` and `/customers/{id}`.
- [x] Refined loyalty page with customer selection, loyalty summary, ledger, and manual point adjustment/redeem handling.
- [x] Added defensive schema repair before Audit/Commercial/Loyalty endpoints so missing runtime tables are repaired before queries.

## Project Workflow Rules

- [x] Every new feature request must be added to `MASTER-TODO.md` before or while implementing it.
- [x] Every bug/error raised by the user must be added to `ISSUES.md` and marked fixed only after the fix is validated.

## GST Accounting Service Integration

- [x] Connect GST Return module with accounting service without removing manual GST draft flow.
- [x] Add GST accounting summary/reconciliation endpoint using accounting ledgers (`Output GST`, `Input GST`).
- [x] Add GST settlement journal posting for GSTR-3B output tax, input ITC, credit carry-forward, interest, and late fee.
- [x] Add GSTR-3B draft-to-accounting posting endpoint with GST draft audit entry.
- [x] Add GST Returns UI panel to refresh accounting summary and post current/saved GSTR-3B to accounting.
- [x] Make database schema repair run even when auto-migration is disabled, so older Docker volumes are repaired before endpoints query newer tables.

## Process Rule Update
- [x] Every requested feature must be added to `MASTER-TODO.md` before or while implementing it.
- [x] Every bug/error raised by the user must be added to `ISSUES.md` and marked fixed after validation.

## Oracle Cloud Secondary Database Sync

- [x] Add feature request to maintain Oracle Cloud Free Tier database as secondary shared sync hub.
- [x] Keep PostgreSQL as primary transactional database and Oracle as common-ground secondary database for other apps.
- [x] Add Oracle sync configuration and Docker/env variables.
- [x] Add Oracle connection test, storage repair, and manual sync endpoints.
- [x] Add background sync service with configurable interval and run-on-startup option.
- [x] Add Oracle hub schema creation for `GARMETIX_SYNC_EVENTS` and `GARMETIX_SYNC_STATE`.
- [x] Add local sync checkpoint/run tables so repeated syncs do not resend every row unnecessarily.
- [x] Add admin UI page `/oracle-sync` for status, connection test, repair, and manual sync.
- [x] Document Oracle sync setup and next guided step for bidirectional/conflict-controlled sync.

## Oracle Cloud Secondary Database Sync - v2

- [x] Add pending Oracle Sync v2 items to TODO: bidirectional sync, conflict rules, Oracle wallet setup, monitoring UI, retry/dead-letter support, and broader sync history.
- [x] Add bidirectional sync direction support (`PushToOracle`, `PullFromOracle`, `Bidirectional`) while keeping PostgreSQL as the primary transactional store.
- [x] Add conflict policy configuration: `ManualReview`, `GarmetixWins`, `OracleWins`, and `LatestWins`.
- [x] Add Oracle wallet/TNS_ADMIN configuration for Oracle Autonomous Database Free Tier.
- [x] Add inbound Oracle event pull into local review queue instead of directly overwriting Garmetix data.
- [x] Add local inbound queue table for Oracle events from other apps.
- [x] Add local dead-letter table for failed push/pull rows and unsupported external entities.
- [x] Add sync run history with pushed/pulled counts.
- [x] Add API endpoints for history, inbound queue, dead letters, retry, and resolve.
- [x] Refine Oracle Sync UI with bidirectional controls, inbound review queue, dead-letter actions, and sync history.
- [x] Define entity ownership matrix for Oracle inbound sync and expose it in API/UI.
- [x] Implement approved inbound merge rules for shared master data (`Company`, `StoreGroup`, `Store`, `Customer`, `Vendor`, `Product`, `ProductCategory`, `ProductSubCategory`, and `Employee`) with admin apply/reject review.
- [x] Block transactional/GST/accounting/stock inbound overwrite by default unless admin explicitly forces apply.
- [x] Add Oracle inbound apply/reject API endpoints and UI actions.
- [ ] Test Oracle bidirectional sync against a real Oracle Cloud Free Tier Autonomous DB and connected external app.
- [ ] After real-app testing, decide whether any entity should be auto-applied instead of manual review.


## Oracle Cloud Secondary Database Sync - v3

- [x] Added entity ownership matrix for Oracle common-hub integration.
- [x] Added inbound apply/reject actions from Oracle review queue.
- [x] Added conflict-aware apply behavior (`ManualReview`, `GarmetixWins`, `OracleWins`, `LatestWins`).
- [x] Shared master data can be merged after admin review.
- [x] Transactional, GST, stock, loyalty-ledger, and accounting entities remain blocked by ownership rules unless force-applied by admin.
- [x] Added Oracle Sync UI ownership table and inbound queue action buttons.
- [ ] Real Oracle Cloud + external app integration test.
- [ ] Final auto-apply policy decision after production data ownership review.

## Oracle Cloud Secondary Database Sync - v4

- [x] Add Oracle Cloud Free Tier readiness checklist endpoint and UI panel.
- [x] Add Oracle wallet/TNS readiness checks for Autonomous DB setup.
- [x] Add trusted-source auto-apply policy matrix for shared master data.
- [x] Add config keys for auto-apply entity allowlist and trusted source application allowlist.
- [x] Add guarded auto-apply endpoint for pending inbound Oracle events.
- [x] Keep transactional/GST/accounting/stock/ledger inbound overwrite blocked by default.
- [ ] Run real Oracle Cloud Free Tier connection test using wallet/TNS settings.
- [ ] Connect one external app to Oracle hub and validate inbound event review/apply.
- [ ] Enable auto-apply only for approved shared master entities after production ownership review.


## Oracle Cloud Secondary Database Sync - v5

- [x] Add Oracle external-app smoke test plan endpoint and UI support.
- [x] Add guarded external-app smoke test that seeds a shared-master `Customer` event into Oracle and pulls it into Garmetix inbound review queue.
- [x] Add scripts and SQL samples for an external app / SQL Developer smoke test.
- [x] Keep smoke test safe by restricting default test to shared master data and review queue, not transactional/GST/accounting data.
- [ ] Run the external-app smoke test against the real Oracle Cloud Free Tier database.
- [ ] Connect a real second app and validate one actual inbound shared-master event.
- [ ] Decide final auto-apply entities after real-app ownership review.

## Sale / Purchase / Inventory Production Hardening

- [x] Export Sale/Purchase/Inventory model/UI/data-layer audit report to PDF. Stored under `docs/reports/`.
- [x] Stage 1 model alignment foundation: add Product HSN field, PurchaseInvoice StoreGroup/Store/SupplierInvoiceDate fields, InvoiceItem product/HSN/unit/category/GST split snapshots, extended InvoicePayment/CardPayment/VendorPayment fields, PurchasePayment allocation model, and StockMovement model.
- [x] Stage 1 database repair foundation: add idempotent schema repair for the new product, purchase, invoice item, payment, purchase payment, and stock movement columns/tables so older PostgreSQL volumes can upgrade safely.
- [x] Stage 2 backend posting foundation: populate sale/purchase item snapshots, calculate CGST/SGST/IGST split, create stock movement rows for sales, purchases, sales returns, exchanges, and purchase returns, store purchase payment allocations, preserve original purchase invoice values on cancellation.
- [x] Stage 3 UI completion: Product Master, customer/vendor picker, salesman selection, split payment UI, billing adjustments, purchase dates, and partial purchase return selection were implemented across Stages 3A-3D.
- [x] Stage 4 core hardening: sequence-safe numbering, PostgreSQL stock locking, bill-discount tax allocation, stock adjustment/transfer/physical count UI, GST reports, consistency checks, and controlled repair were implemented across Stages 4A-4E.
- [ ] Stage 8 inventory completion: formal stock-operation documents, authoritative movement-ledger balance, documented stock valuation method, and automated reconciliation/concurrency coverage.
- [ ] Stage 8 purchase-return completion: formal Purchase Return header/items, exact item-level ITC reversal, vendor refund settlement, and dedicated return/debit-note print linkage.

## Stage 3A Product Master - added 2026-06-08

- Applied updated enum direction: obsolete `GarmentCategory` and legacy enum `ProductCategory`; added `ProductGroup`, corrected `ProductType.Readymade`, and added Tailoring/Trims/PromoItems/Shoes.
- Added ProductGroup/StockType/category/subcategory metadata to inventory models and runtime schema repair.
- Added `/api/inventory/product-master` endpoints for combined product + stock + product detail create/update.
- Reworked Inventory page into Product Master UI with HSN, GST, group/type, category/subcategory, vendor, brand, style, color, cost, and stock type.
- Validation completed through the Stage 7M backend and Nuxt production builds.

## Stage 7M and Stage 8 Planning

- [x] Complete Stage 7M Pre-v4.0 UI Naming and Menu Cleanup.
- [x] Preserve the active grouped Nuxt UI shell and `NUXT_PUBLIC_DASHBOARD_SHELL=legacy` rollback.
- [x] Keep Cash Vouchers separate under Off Book and regular vouchers under Accounting.
- [x] Complete Stage 8A full-page UI audit and standardization.
  - [x] Package 1: persistent UI audit queue and standardized Credit Note / Debit Note registers.
  - [x] Package 2: standardized Commercial Notes / Customers and synchronized runtime identity to v4.0.0.
  - [x] Package 3: standardized Parties / Vouchers, added voucher-type filtering and sanitized retryable register errors, and synchronized runtime identity to v4.0.1.
  - [x] Package 4: standardized Loyalty / Petty Cash, preserved daily-cash automation and A5 printing, and synchronized runtime identity to v4.0.2.
  - [x] Package 10: standardized reporting and data-operation registers and synchronized runtime identity to v4.0.8.
  - [x] Package 11: repaired Product Master transactions and centralized persistent API, frontend, browser, and background-service Message Logs in v4.0.9.
  - [x] Package 12: standardized Cash Vouchers with retained errors, type filtering, local dates, and preserved Off Book isolation in v4.0.10.
  - [x] Package 13: completed independent Off Book Non-GST sale, purchase, stock, settlement, server PDF, QR lookup, and regular-book exclusion in v4.0.11.
  - [x] Package 14: standardized Company Setup, Client Onboarding, AF/SS Defaults, and Roles & Users with retryable states, responsive tables, wide forms, local-date-safe setup records, and cleaned business copy in v4.0.12.
  - [x] Package 15: standardized Production Readiness, Release Stabilization, Data Consistency, and Oracle Sync with retained failures, retry actions, loading states, responsive surfaces, and safe Oracle filter values in v4.0.13.
  - [x] Package 16: standardized GST Returns, Profile, About Garmetix, Contact Us, and Help and FAQ; fixed local GST dates and FAQ filter values; and completed the Stage 8A audit queue in v4.0.14.
- [x] Complete Stage 8B role, permission, and user-administration hardening.
  - [x] Package 1: added active/inactive user lifecycle, dedicated password administration, server-owned Admin role flags, last-admin safeguards, immediate inactive-session blocking, and security audit events in v4.1.0.
  - [x] Package 2: centralized backend authorization, displayed the effective role matrix, added HR/Payroll roles and specialist landing routes, hardened access import/export, and added ten role permission tests in v4.1.1.
  - [x] Package 3: completed permission-aware desktop/mobile shell behavior and added a scoped business notification and quick-action menu without technical log noise in v4.1.2.
- [x] Complete Stage 8C purchase return and vendor settlement.
  - [x] Package 1: added formal purchase-return header/item records, server-owned return numbering, immutable purchase/tax snapshots, linked stock movements and debit notes, legacy return compatibility, and a searchable return register in v4.2.0.
  - [x] Package 2: added complete A4/A5 purchase-return/debit-note PDFs, stable QR lookup, automatic first print, direct reprint/download actions, and persistent print audit state in v4.2.1.
  - [x] Package 3: added vendor debit-note allocation, cash/bank refund receipts, mixed settlements, outstanding purchase allocation, settlement history, and linked voucher/journal/bank audit records in v4.2.2.
  - [x] Package 4: added immutable item-level ITC reversal, exact GST component rounding, full return-to-stock-to-debit-note-to-journal reconciliation, audit entries, and a visible reconciliation workspace in v4.2.3.
- [ ] Complete Stage 8D inventory documents and valuation.
  - [x] Package 1: added formal adjustment, transfer, and physical-count documents, item snapshots, movement links, server numbering, QR lookup, and a searchable document register in v4.3.0.
  - [x] Package 2: made the regular movement ledger authoritative, added weighted-average valuation snapshots, repaired missing import/cancellation movements, added projection reconciliation, and covered valuation rules with tests in v4.3.1.
  - [x] Package 3: added balanced accounting for stock excess, shortage, write-off, and transfer; store-wise inventory ledgers; linked journal status; a formal write-off workflow; and the single sidebar-footer Notifications action in v4.3.2.
  - [x] Package 4: restored vertical sidebar-footer Status and Notifications actions and added dedicated stock ageing, low-stock risk, weighted-average valuation, projection reconciliation, filtering, and CSV reporting in v4.3.3.
  - [x] Package 5: serialized sequence generation and stock transfers with transaction-scoped PostgreSQL locks, enforced one active sequence row, formalized negative-stock behavior, and added live PostgreSQL concurrency tests in v4.3.4.
- [ ] Complete Stage 8E accounting and payment hardening.
  - [x] Package 5: hardened automatic Party and Bank Account ledger synchronization with health checks and repair workflow in v4.4.2.
  - [x] Package 6: added customer/vendor due dashboards, cash/payment summaries, and store-group comparison views in v4.4.3.
- [ ] Complete Stage 8F automated tests and deep audit history.
- [x] Complete Stage 8G real integrations and production go-live validation.

## Sales Invoice Stability - added 2026-06-16

- [x] Replace the New Invoice modal with a stable full-page `/billing/new` workflow.
- [x] Repair the PostgreSQL retry execution-strategy failure for sale create, cancel, return and exchange.
- [x] Add mobile customer lookup, automatic customer creation, GSTIN/B2B/interstate handling and Manager default selection.
- [x] Add resilient invoice drafts, percent/amount discounts, requested item columns and responsive payment controls.
- [x] Round bill/payment totals, preserve customer balances and adjustments, and automatically print after creation.
- [x] Reconcile the remaining June 15 operational TODO items into their implementation stages.

## Product Save and Central Message Logging - added 2026-06-14

- [x] Reproduce and fix Product Master create/edit failures against the current PostgreSQL schema.
- [x] Persist unhandled API exceptions and failed HTTP responses to Message Logs with trace, user, workspace, route, and status context.
- [x] Persist successful state-changing API events without storing request bodies or credentials.
- [x] Persist frontend action failures, UI messages, browser errors, and unhandled promise rejections to Message Logs.
- [x] Persist Garmetix background-service and application `ILogger` messages through a bounded non-recursive queue.
- [x] Keep Message Log diagnostic details restricted to authorized administrators while allowing authenticated users to submit their own client events.


## Stage 8E Package 7 / v4.5.0

- [x] Package 7: added tailoring/stitching and alteration workflow before Stage 8F, including orders, delivery, service invoice conversion, customer receipts, vendor payments, schedule/history, and in-house alteration profit impact.

- [x] Stage 8G Package 1: added Backup Maintenance Center, verify-all, safe cleanup and Mac mini backup scripts.

- [x] Stage 8G Package 4: added Google Drive off-site backup validation workflow.

- [x] Stage 8G Package 5: added GSTIN provider validation workflow.

- [x] Stage 8G Package 6: added Oracle Cloud wallet/TNS and external-app sync validation runbook and Mac mini readiness script.

- [x] Stage 8G Package 7: added production security hardening and Docker log retention checks in v4.6.6.

- [x] Stage 8G Package 9: added final go-live acceptance, role matrix, production release checklist and stage-completion script.

- [x] Stage 8H Package 1: post-go-live acceptance stabilization for role/menu, HR/payroll, attendance, purchase inward and vendor-payment acceptance.

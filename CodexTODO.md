# Codex TODO - Garmetix Web

Use this file as the running handoff checklist. When a task is completed, mark it with `[x]` and add a short note/date if useful.

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

- [x] Local backup and restore.
- [x] Google Drive credential setup. Added service-account JSON configuration and Docker secret mount.
- [x] Upload backups to Google Drive. Local manual/scheduled backups can upload automatically or manually from System Health.
- [x] Restore/download backups from Google Drive. Added cloud list/download/delete/restore endpoints and UI actions.
- [x] Show online backup status and schedule history. System Health now shows Google Drive status, cloud files, last error/action, and online backup count.

## Billing / Invoice Printing

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

- [ ] Run full backend build on developer machine.
- [ ] Run full Nuxt build on developer machine.
- [ ] Test Docker Compose clean install.
- [ ] Test fresh database migration.
- [ ] Test backup/restore.
- [ ] Test permissions with Admin, Owner, Manager, Cashier, Inventory, HR, Payroll, Accountant users.


## Current validation note

- Backend and full Nuxt SSR builds still need to be run on the developer machine because this sandbox does not include `dotnet` and external Nuxt font/icon fetches keep failing here.
- Use the commands in `backend/Developer-Validation-Checklist.md` after extracting the ZIP.

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

- [x] Every new feature request must be added to this `CodexTODO.md` before or while implementing it.
- [x] Every bug/error raised by the user must be added to `CodexISSUES.md` and marked fixed only after the fix is included in a generated ZIP.

## GST Accounting Service Integration

- [x] Connect GST Return module with accounting service without removing manual GST draft flow.
- [x] Add GST accounting summary/reconciliation endpoint using accounting ledgers (`Output GST`, `Input GST`).
- [x] Add GST settlement journal posting for GSTR-3B output tax, input ITC, credit carry-forward, interest, and late fee.
- [x] Add GSTR-3B draft-to-accounting posting endpoint with GST draft audit entry.
- [x] Add GST Returns UI panel to refresh accounting summary and post current/saved GSTR-3B to accounting.
- [x] Make database schema repair run even when auto-migration is disabled, so older Docker volumes are repaired before endpoints query newer tables.

## Process Rule Update
- [x] Every requested feature must be added to `CodexTODO.md` before/while implementation.
- [x] Every bug/error raised by user must be added to `CodexISSUES.md` and marked fixed after the generated ZIP includes the fix.

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

- [x] Export Sale/Purchase/Inventory model/UI/data-layer audit report to PDF. Added `Garmetix_Sale_Purchase_Inventory_Model_Audit_Report.pdf` as a separate handoff document.
- [x] Stage 1 model alignment foundation: add Product HSN field, PurchaseInvoice StoreGroup/Store/SupplierInvoiceDate fields, InvoiceItem product/HSN/unit/category/GST split snapshots, extended InvoicePayment/CardPayment/VendorPayment fields, PurchasePayment allocation model, and StockMovement model.
- [x] Stage 1 database repair foundation: add idempotent schema repair for the new product, purchase, invoice item, payment, purchase payment, and stock movement columns/tables so older PostgreSQL volumes can upgrade safely.
- [x] Stage 2 backend posting foundation: populate sale/purchase item snapshots, calculate CGST/SGST/IGST split, create stock movement rows for sales, purchases, sales returns, exchanges, and purchase returns, store purchase payment allocations, preserve original purchase invoice values on cancellation.
- [ ] Stage 3 UI completion: add full Product master form, customer/vendor picker, salesman selector, payment split form, card/UPI/cheque detail fields, apply advance/credit-note/loyalty redemption in billing, supplier invoice date/due date fields in purchase, and partial purchase return item-selection UI.
- [ ] Stage 4 business hardening: sequence-safe invoice numbers, stock concurrency/row-locking, bill-level discount tax allocation, stock adjustment/transfer/physical count screens, stock valuation method, and automated test coverage.

## Stage 3A Product Master - added 2026-06-08

- Applied updated enum direction: obsolete `GarmentCategory` and legacy enum `ProductCategory`; added `ProductGroup`, corrected `ProductType.Readymade`, and added Tailoring/Trims/PromoItems/Shoes.
- Added ProductGroup/StockType/category/subcategory metadata to inventory models and runtime schema repair.
- Added `/api/inventory/product-master` endpoints for combined product + stock + product detail create/update.
- Reworked Inventory page into Product Master UI with HSN, GST, group/type, category/subcategory, vendor, brand, style, color, cost, and stock type.
- Pending validation: run `dotnet build`, `npm install`, and `npm run build` in the Windows/Docker dev environment.


## Stage 3B completed in this package

- Added billing options/customer profile endpoints.
- Added existing customer picker support in billing UI.
- Added salesman picker support and default quick-setup salesman seed.
- Added split payment rows in billing UI.
- Added customer adjustment application support for credit balance, credit note, advance receipt, and loyalty redemption.
- Added receipt display of payment references and adjustment source.

## Next recommended package

Stage 3C — Purchase UI Completion.

Suggested scope:

1. Vendor picker with GSTIN profile summary.
2. Supplier invoice date and due date fields in purchase inward UI.
3. Purchase item HSN/unit/tax snapshot visibility.
4. Purchase payment allocation/history panel.
5. Freight taxable/non-taxable fields for later landed-cost logic.

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

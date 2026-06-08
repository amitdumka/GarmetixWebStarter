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
- [ ] Connect to final licensed GSTIN provider credentials in production `.env`.

## GST Returns - URGENT

- [x] Create standalone GST Return module shell without linking Billing/Purchase. Added `/gst-returns` frontend page and `/api/gst-returns` backend group.
- [x] Generate GSTR-1 JSON from manual/separate entry data.
- [x] Generate GSTR-1 Excel from manual/separate entry data.
- [x] Generate GSTR-3B JSON from manual/separate entry data.
- [x] Generate GSTR-3B Excel from manual/separate entry data.
- [x] Review generated JSON/Excel against latest GST portal/offline utility templates before production filing. Added schema-review endpoint, Excel checklist, stronger GSTIN/POS/rate/tax validations, and portal-validation warnings.
- [x] Add saved GST return drafts and audit trail. Added `GstReturnDrafts` and `GstReturnAuditEntries`, save/load/delete/mark-filed flows, draft export, and audit panel.
- [ ] Link GST module with Billing/Purchase after manual module approval.

## Reports

- [x] PDF export for reports. Added PDF/Save-as-PDF action through report print layout.
- [x] Excel export for reports. Added Excel `.xls` export from current report rows.
- [x] Print reports. Existing report print action now also caches the snapshot before printing.
- [x] Cache repeated/same report results. Added local report snapshot cache/load/live controls keyed by report/filter/search.

## Purchase

- [ ] Purchase invoice list and detail/print view.
- [ ] Purchase return/cancellation with stock reversal.
- [ ] Vendor payment voucher creation from purchase payment.
- [ ] Better tax/category selector in purchase inward form.

## Sales Return / Exchange

- [ ] Partial sales return.
- [ ] Exchange item flow.
- [ ] Return voucher/credit note.
- [ ] Stock reversal for selected returned items only.
- [ ] Customer balance adjustment.

## Import / Export

- [ ] CSV/Excel import UI.
- [ ] Export UI.
- [ ] Validation preview before import.
- [ ] Error report download.
- [ ] Admin import/export endpoint polish.

## Audit UI / Activity Logs

- [ ] Filter audit by module/user/date/action.
- [ ] View changed fields.
- [ ] Export audit report.

## Testing / Deployment Hardening

- [ ] Run full backend build on developer machine.
- [ ] Run full Nuxt build on developer machine.
- [ ] Test Docker Compose clean install.
- [ ] Test fresh database migration.
- [ ] Test backup/restore.
- [ ] Test permissions with Admin, Owner, Manager, Cashier, Inventory, HR, Payroll, Accountant users.

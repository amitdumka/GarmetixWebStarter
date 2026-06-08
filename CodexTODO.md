# Codex TODO - Garmetix Web

Use this file as the running handoff checklist. When a task is completed, mark it with `[x]` and add a short note/date if useful.

## Auth / Login

- [x] Remove "Admin is available" message.
- [x] Add Forgot Password from login screen.
- [x] Add Reset Password from login screen.
- [x] Add Change Password in current user profile.
- [x] Add production email delivery for forgot-password reset link/token. SMTP sender added; SMS still optional for future.
- [ ] Add optional reset-token persistence/revocation table for stricter production security.

## Company / Store / StoreGroup Permission Flow

- [ ] Show Company, StoreGroup, and Store based on logged-in user permissions only.
- [ ] Auto-select default company/store after login.
- [ ] Add clean topbar flow to change company/store group/store.
- [ ] Block module transactions when no valid working store is selected.

## Backup / Restore

- [x] Local backup and restore.
- [ ] Google Drive credential setup.
- [ ] Upload backups to Google Drive.
- [ ] Restore/download backups from Google Drive.
- [ ] Show online backup status and schedule history.

## Billing / Invoice Printing

- [ ] Standard A4 invoice print layout.
- [ ] Standard A5 invoice print layout.
- [ ] Thermal 2-inch receipt layout.
- [ ] Thermal 3-inch receipt layout.
- [ ] Download PDF invoice.
- [ ] Reprint/copy options for invoices.

## Reports

- [ ] PDF export for reports.
- [ ] Excel export for reports.
- [ ] Print reports.
- [ ] Cache repeated/same report results.

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

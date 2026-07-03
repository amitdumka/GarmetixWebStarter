# Garmetix Pending TODO — after v4.12.20

Base at creation: v4.12.20 / Stage 11D-105 Day Book + Pending TODO Register.

## Critical live QA / deployment

- [ ] Deploy latest package and verify API/web build.
- [ ] Run app-info/version check after deploy.
- [ ] Review Message Logs after first login and first page refresh.
- [ ] Run database schema repair if any drift appears.

## Vyapar Sale Import

- [ ] Live preview both Vyapar files.
- [ ] Verify invoice count, item count, fully matched count, missing barcode count.
- [ ] Verify bank/POS/UPI column mapping posts to exact mapped Garmetix bank account.
- [ ] Verify PaymentStatus = Paid + Balance = 0 discount adjustment.
- [ ] Verify historical stock bridge for old sale dates.
- [ ] Confirm import batch and validate imported invoice list.
- [ ] Test batch undo and re-upload after clearing cancelled import history.
- [ ] Add final import summary PDF/Excel report with GST/payment/customer/stock summary.

## Sale / Billing

- [ ] Test sale invoice list filters and pagination on real data.
- [ ] Test mixed payment split in day closing and petty cash sheet.
- [ ] Test revise sale invoice flow with mixed payment.
- [ ] Test invoice replacement approval/reversal stock/accounting/GST impact.
- [ ] Test admin hard-delete for cancelled test invoices only.

## Purchase Inward Date / Vendor Payment

- [ ] Test new purchase inward date in create and edit flows.
- [ ] Verify purchase stock movement date equals inward date.
- [ ] Test vendor payment view/edit/delete.
- [ ] Test vendor payment cash-to-bank and bank-to-cash correction.
- [ ] Run vendor payment reconciliation report and repair API.
- [ ] Verify vendor paid amount and purchase invoice status after edits/deletes.

## Accounting / Day Book

- [ ] Test Day Book default Today view.
- [ ] Test Day Book filters: Today, Yesterday, Month, Last Month, Year, Month-Year, Custom.
- [ ] Test Day Book transaction types: Sales, Purchase, Vouchers, Payments, Journal.
- [ ] Click sale/purchase/voucher/payment rows and verify detail drawer opens directly.
- [ ] Add deeper source-page anchors later so source pages auto-open their edit/detail modal from querystring.
- [ ] Add export Day Book to Excel/PDF if needed.

## Print/PDF

- [ ] Real Sale A4 print.
- [ ] Real Sale A5 print.
- [ ] Real Purchase A4 print.
- [ ] Real Purchase A5 print.
- [ ] Large invoice pagination.
- [ ] Amount box/footer/signature/page summary.
- [ ] Save final print evidence from Print Final Acceptance page.

## Payroll

- [ ] Runtime payroll finalization test on one real month.
- [ ] Verify full/half/absent day calculation.
- [ ] Verify late penalty and OT multiplier.
- [ ] Verify salary payment posting and payslip PDF.
- [ ] Verify payroll/month lock behavior.

## Imports already prepared

- [ ] Apply Royalwood International import if not already applied.
- [ ] Validate vendor, purchase invoice, 58 stock qty and total ₹161,611.70.

## Future feature ideas

- [ ] Day Book Excel/PDF export.
- [ ] Day Book direct edit/view modal for each source transaction without leaving page.
- [ ] Batch-level accounting/GST reconciliation for Vyapar imports.
- [ ] Import rollback report and approval workflow.
- [ ] Mobile-friendly quick Day Book for owner/accountant.

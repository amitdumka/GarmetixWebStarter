# Garmetix Operator User Manual

Version: Stage 5C / v1.6

This manual is for store operators, billing users, inventory users, and managers during production rollout.

## 1. Daily opening checklist

1. Login with your assigned user.
2. Confirm the correct company, store group, and store are selected in the top workspace selector.
3. Open **System Health** and confirm API/database are healthy.
4. Open **Production Readiness** before first go-live and resolve all critical warnings.
5. Open **Release Stabilization** and run smoke checks.
6. Confirm barcode scanner, printer, and internet connection are working.
7. Take a backup before first live billing of the day when the store is newly deployed.

## 2. Product master and stock

Use **Inventory** for product master maintenance.

Mandatory fields for reliable GST and stock reports:

- Product name
- Barcode
- HSN code
- Product type
- Product group
- Category and subcategory
- Unit
- Tax rate and tax type
- MRP
- Cost price/opening quantity when adding opening stock

Use **Stock Ops** for stock adjustment, stock transfer, and physical stock count. Always enter clear remarks so the stock movement ledger remains auditable.

## 3. Billing workflow

1. Open **Billing**.
2. Search/select existing customer or enter walk-in customer details.
3. Select salesman.
4. Scan barcode or search product.
5. Confirm quantity, MRP, discount, tax, and stock availability.
6. Enter bill-level discount only when approved by store policy.
7. Add one or more payment rows: Cash, UPI, Card, Wallet, Cheque, NEFT, RTGS, IMPS, or credit adjustments.
8. Verify total paid amount and balance.
9. Save invoice and print/download receipt.

For card/UPI payments, fill reference/gateway details when available. For credit sale, ensure the customer is correct before saving.

## 4. Sales return and exchange

Use **Sales Return** for customer returns and exchanges.

- Search original invoice.
- Select items and return quantities carefully.
- Confirm refund mode or credit balance handling.
- Print/save the return document.
- Do not manually adjust stock for the same returned item unless instructed by admin.

## 5. Purchase inward

1. Open **Purchase**.
2. Select vendor.
3. Enter supplier invoice number and supplier invoice date.
4. Enter due date.
5. Scan/add products with HSN, unit, product type/group, cost price, MRP, discount, and tax.
6. Enter freight where applicable.
7. Enter purchase payment details if paid immediately.
8. Save inward and print/download purchase receipt.

## 6. Partial purchase return

Use **Purchase Return** for item-wise return to supplier.

- Search purchase invoice.
- Enter return quantity only for the items being returned.
- Review taxable, tax, and total return amount.
- Save partial return/debit note.
- Do not use full cancel after item-wise return has already started.

## 7. GST reports

Use **GST Reports** for operational review:

- HSN summary
- Tax-rate reconciliation
- Invoice register
- CSV exports

Before filing GST, run **Consistency & Repair** and verify no critical GST/header/item mismatch remains.

## 8. Backup and restore

Use **System Health** for backup verification and restore preflight.

Rules:

- Always verify backup checksum before restore.
- Always run restore preflight before restore.
- Only admin should type `RESTORE` and start restore.
- Do not use the application while restore is running.

## 9. Data consistency and repair

Use **Consistency & Repair** only as admin.

Recommended order:

1. Run summary.
2. Review issues.
3. Preview repair action.
4. Apply repair only when preview is understood.
5. Re-run consistency summary.
6. Take backup after successful repair.

## 10. Daily closing

1. Complete all pending sales/purchase entries.
2. Review unpaid/credit invoices.
3. Review stock movement exceptions.
4. Run data consistency summary.
5. Export important reports if needed.
6. Take closing backup.
7. Logout.

## 11. Support log information

When reporting an issue, send:

- Screenshot of the page and error.
- Invoice/purchase number if relevant.
- Time of issue.
- User/store name.
- Docker build/runtime logs if deployment related.
- Data consistency issue code if related to data mismatch.

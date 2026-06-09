# Sale / Purchase / Inventory Hardening Notes

This package starts the next production-hardening phase based on the Sale/Purchase/Inventory audit report.

## Implemented in this package

### Model alignment foundation

- Added `Product.HSNCode` so HSN can be stored at product level, not only stock level.
- Added purchase invoice working scope fields:
  - `PurchaseInvoice.StoreGroupId`
  - `PurchaseInvoice.StoreId`
  - `PurchaseInvoice.SupplierInvoiceDate`
- Added invoice-item historical snapshot fields:
  - `ProductName`
  - `HSNCode`
  - `Unit`
  - `ProductCategoryId`
  - `ProductSubCategoryId`
  - `CGSTAmount`
  - `SGSTAmount`
  - `IGSTAmount`
- Extended payment models:
  - `InvoicePayment.BankAccountId`
  - adjustment source fields
  - gateway/settlement fields
  - safer card reference fields
  - purchase invoice link on vendor payment
- Added `PurchasePayment` allocation model so purchase paid/balance is not only inferred from journal lines.
- Added `StockMovement` model for auditable stock inward/outward/return movements.

### Backend posting foundation

- Sales now populate invoice item snapshots and GST split values.
- Sales create stock movement rows for sale outward.
- Sales returns create stock movement rows for return inward.
- Exchange replacement sales create stock movement rows and now include applied store credit in `PaidAmount`.
- Purchase inward now populates item snapshots, product HSN/unit, purchase invoice store fields, supplier invoice date, GST split values, stock movement rows, and purchase payment allocation rows.
- Vendor payment voucher now also writes `PurchasePayment` allocation rows.
- Purchase paid amount lookup now prefers `PurchasePayments`, with journal fallback for older data.
- Purchase cancellation now preserves original invoice values for audit instead of zeroing invoice totals.

### Database repair

Startup schema repair was extended for older PostgreSQL volumes so the new columns/tables are created safely with `ADD COLUMN IF NOT EXISTS` and `CREATE TABLE IF NOT EXISTS`.

## Still pending

- Full product master UI with HSN/unit/category/product type/description/brand/style/vendor.
- Customer/vendor picker and salesman selection in billing.
- Payment split UI and card/UPI/cheque detail capture.
- Apply customer advance, credit note, and loyalty redemption during billing.
- Supplier invoice date/due date fields in purchase UI.
- Item-wise partial purchase return UI and API.
- Sequence-safe invoice numbering.
- Stock concurrency protection / row lock.
- Bill discount tax allocation across items.
- Stock adjustment, transfer, and physical count screens.

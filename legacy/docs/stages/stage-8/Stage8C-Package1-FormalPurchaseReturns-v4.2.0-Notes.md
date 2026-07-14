# Stage 8C Package 1 - Formal Purchase Return Records

Version: 4.2.0  
Date: 2026-06-15

## Delivered

- Added formal `PurchaseReturn` and `PurchaseReturnItem` persistence.
- Added server-owned `StoreCode/YYYYMM/PR/series` return numbering.
- Preserved original purchase, vendor, product, barcode, HSN, unit, category, quantity, rate, discount, GST split, reason, and amount snapshots.
- Linked new stock-return movements and generated debit notes to the formal return document.
- Applied the same formal-document flow to full purchase cancellation.
- Kept pre-v4.2 stock-movement-only returns in returnable-quantity calculations.
- Added scoped recent-return and return-detail API endpoints.
- Added a searchable Purchase Return register and item-snapshot detail modal.
- Added an idempotent migration and startup schema repair for existing Docker databases.

## Compatibility

- Existing purchase invoices and historical stock movements are not rewritten.
- Existing partial returns continue to reduce available return quantity.
- New returns use formal records as the source for audit and future settlement/print work.

## Next

Stage 8C Package 2 will add the dedicated purchase-return/debit-note PDF, QR lookup, print-on-create flow, and return-document status controls.

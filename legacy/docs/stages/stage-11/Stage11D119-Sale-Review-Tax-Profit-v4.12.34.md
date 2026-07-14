# Stage 11D-119 — Sale Review Tax and Profit

Version: 4.12.34

## Added

- New **Sales → Sale Review** page at `/billing/sale-review`.
- New backend endpoint: `GET /api/sale-review`.
- Apparel GST threshold review:
  - Unit basic price up to ₹2,499 uses expected GST 5%.
  - Unit basic price ₹2,499.01 and above uses expected GST 18%.
  - Flags invoices/items where charged tax differs from expected threshold tax.
- Computes proposed extra amount to review when 18% was charged on items whose unit basic price is within the 5% slab.
- Adds item-wise and invoice-wise profit/loss using stock movement cost from sale stock-out rows.
- CSV export and accountant copy summary from the Sale Review page.

## Notes

This stage is intentionally review-only. It does not automatically alter historical sale invoices or post Extra Amount receipts. The adjustment-posting workflow should be confirmed with the accountant before adding a write action.

# Stage 8A Package 13 - Independent Off Book Goods

Version: 4.0.11
Build: `GARMETIX-8A-20260614-4011`
Date: 2026-06-14

## Completed

- Removed Non-GST sale and purchase posting from ledgers and journals.
- Removed historical Non-GST journal entries and internal ledger links through migration and schema repair.
- Added independent paid and balance tracking.
- Standardized monthly document numbers as `StoreCode/YYYYMM/NGP/series` and `StoreCode/YYYYMM/NGS/series`.
- Added create, edit, delete, stock reversal, reporting, and store-consistency validation.
- Added colored A4/A5 server PDFs with all line items and QR lookup.
- Rebuilt the Nuxt page as a register-first workspace with wide sale and purchase entry.
- Excluded Off Book stock from Product Master, Billing, Purchase, Stock Operations, regular inventory import/export, and on-book dashboard totals.
- Kept Off Book sales, purchases, stock, and result visible only as clearly separate dashboard comparisons.

## Validation

- Backend build: passed with zero warnings.
- Nuxt production build: passed.
- Docker runtime, API transaction isolation, PDF, QR, and responsive browser checks are recorded in the validation log after deployment.

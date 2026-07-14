# Stage 14L.2 - Books Day Book Port

## Scope

Frontend-only modular Books port of the legacy Accounting menu Day Book.

## Added

- `apps/books/pages/day-book.vue`.
- `/day-book` route registry entry under Books Accounting.
- Books sidebar menu entry and Books Home quick link.
- Version bump to `6.8.3`.

## Features

- Single-date, today, yesterday, this-month, last-month, this-year, month/year and custom range filters.
- Transaction type filter, optional journal rows, search and server pagination.
- Summary cards for transaction count, credit/inflow, debit/outflow and net amount.
- CSV export through `GET /api/day-book/export.csv`.
- Print/PDF evidence through `GET /api/day-book/print`.
- Transaction detail slideover using detail paths returned by the API.
- Quick-create actions for Sale, Purchase Inward, Vendor Payment, Vendor Advance, Book Voucher and Cash Voucher.
- Modular source-link mapping so Day Book source actions open the owning app instead of legacy-only paths.

## Source Ownership Mapping

- Sale invoice and customer receipt rows: POS Sales History.
- Purchase inward rows: Main Purchase.
- Voucher, vendor payment and accounting journal rows: Books.
- Cash voucher rows: POS Off Book.

## Notes

- No backend, database, GST module, pull, commit, push or deployment change was made.
- Claude was noted as actively working on GST; this pass avoided GST files except normal route/sidebar adjacency.

## Validation

- `npm --prefix frontend/modular --workspace @garmetix/books-web run build`

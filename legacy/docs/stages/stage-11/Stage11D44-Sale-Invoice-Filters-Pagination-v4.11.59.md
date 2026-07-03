# Stage 11D-44 — Sale Invoice Filters + Pagination — v4.11.59

## Purpose

Prevent the Sales Billing invoice register from loading broad invoice history by default. The page now defaults to **Today** and uses server-side date filtering, search, status filtering, totals and pagination.

## Backend

Added paged endpoint:

```http
GET /api/billing/sales
```

Supported query parameters:

- `datePreset=today|yesterday|month|last-month|year|month-year|custom`
- `year=2026`
- `month=6`
- `from=2026-06-01`
- `to=2026-06-26`
- `q=invoice/customer/mobile/gstin`
- `status=all|pending|paid|partiallyPaid|cancelled|refunded|partiallyRefunded|overdue|draft`
- `page=1`
- `pageSize=25|50|100|200`

Default behavior is `datePreset=today`, `page=1`, and `pageSize=50`.

## Frontend

Updated:

```text
frontend/garmetix-web/pages/billing/index.vue
```

The Sales Billing register now has:

- Today default filter
- Yesterday
- This month
- Last month
- This year
- Month-year selector
- Custom from/to date selector
- Server-side status filter
- Debounced server-side search
- Page size selector
- Previous/Next pagination
- Server-side summary cards for the selected date/status/search scope

## Compatibility

The old endpoint remains available:

```http
GET /api/billing/sales/recent
```

This keeps dashboard and sales-return pages compatible.

## No migration

No new database table or migration was added.

# Stage 11D-102 — Purchase/Vendor Payment Live QA Fixes

Version: `4.12.17`
Base: `v4.12.16 Stage 11D-101 Purchase Vendor Payment Runtime QA`

## Purpose

Stage 11D-102 makes vendor payment maintenance safer for live data correction. Stage 11D-100 added view/edit/delete, and Stage 11D-101 added DB QA checks. This stage adds server-side filters and pagination so vendor payment maintenance does not stay limited to a fixed recent list.

## Added

### Backend

New paged vendor payment register API:

```txt
GET /api/purchase/payments
```

Supports:

```txt
year
month
from
to
page
pageSize
q
paymentMode
vendorId
includeDeleted
```

Default behavior is safe:

```txt
current month
page=1
pageSize=50
includeDeleted=false
```

The response includes:

```txt
items
total
page
pageSize
year
month
amount
cashAmount
nonCashAmount
activeCount
deletedCount
```

The old endpoint remains for compatibility:

```txt
GET /api/purchase/payments/recent
```

### Frontend

Updated:

```txt
frontend/garmetix-web/pages/purchase/index.vue
```

Vendor Payments panel now has:

- search by vendor/invoice/reference/remarks
- month and year filter
- payment mode filter
- page size selector
- previous/next pagination
- cash/non-cash/payment totals for selected filter

## Why this matters

Vendor payment entries can be edited or deleted, but a fixed recent list can hide older wrong entries. The register now remains manageable and searchable for correction without loading all history.

## Validation

Run:

```bash
python3 scripts/validation/stage11d102-purchase-vendor-payment-live-qa-fixes-check.py
```

Then deploy/test:

```bash
docker compose build --no-cache
docker compose up -d
```

## Runtime test checklist

1. Open Purchase page.
2. Vendor Payments should show current month by default.
3. Search by vendor name, supplier invoice number, UTR/reference, and remarks.
4. Filter by Cash, UPI, NEFT, RTGS, Cheque.
5. Move between pages using Previous/Next.
6. Edit a vendor payment from a non-first page.
7. Delete a wrong vendor payment and verify it disappears from active view.
8. Run Stage 11D-101 DB-only validation again after corrections.

## Next part

`Stage 11D-103 — Purchase/Vendor Payment Live Error Fixes`

This should be driven by your live testing logs after trying vendor payment filters, edit, delete and purchase inward date changes.

# Stage 11D-114 — Purchase Inward Final QA Polish

Version: v4.12.29  
Build: GARMETIX-11D114-20260630-4229  
Date: 2026-06-30

## Purpose

This stage polishes the Purchase Inward register after the Day Book and Vendor Payment fixes. It focuses on the practical store workflow: purchase inward date should control the register by default, inward date and entry date should be visible separately, and Day Book return flow should remain intact when creating or revising an inward.

## Completed changes

- Purchase register now defaults to **Inward date** basis instead of silently filtering only by entry date.
- Added date basis selector:
  - Inward date
  - Entry date
- Purchase register table now shows:
  - Supplier Invoice
  - Inward No.
  - Inward Date
  - Entry Date
  - Vendor
  - Amount / Paid / Balance / Status
- Backend `/api/purchase/invoices` now accepts `dateMode=inward` or `dateMode=entry`.
- Backend default remains inward-date friendly for normal purchase inward work.
- Purchase invoice ordering follows the selected date basis.
- Existing edit inward-date behavior remains protected: changing inward date also moves linked purchase stock movement dates.
- Purchase page supports extra deep-link aliases:
  - `/purchase?purchaseInvoiceId=<id>`
  - `/purchase?inwardId=<id>`
  - `/purchase?invoiceId=<id>`
  - `/purchase?editPurchaseInvoiceId=<id>`
- Purchase receipt popup is wider for practical A4/item review.
- New/revised purchase inward keeps **Back to Day Book** return hint using `fromDayBook=1`.
- Purchase new page returns to `/purchase?fromDayBook=1` when opened from Day Book.

## Live QA checklist

1. Open `/purchase` and confirm default date basis is **Inward date**.
2. Select an older inward month and confirm old inward rows remain visible by inward date.
3. Switch date basis to **Entry date** and confirm register changes based on entry date.
4. Open any purchase inward and confirm the receipt drawer is wide enough for item rows.
5. Edit inward date and save.
6. Confirm linked stock movement date follows the new inward date.
7. Open `/purchase?inwardId=<id>` and confirm the correct inward opens.
8. Open `/purchase?editPurchaseInvoiceId=<id>` and confirm edit form opens.
9. From Day Book, open purchase inward, revise/create inward, and return without losing the Day Book state.

## Validation

Run:

```bash
python3 scripts/validation/current-release-checks.py
```

Expected result:

```text
Stage 11D-114 validation passed
Current release validation passed for Stage 11D-114 Purchase Inward Final QA Polish / v4.12.29.
```

## Next recommended stage

Stage 11D-115 should move to **Sale Invoice final QA polish**:

- sale invoice filters/pagination live QA
- A4/A5 print final checks
- mixed payment split check
- Day Book sale receipt/customer receipt return behavior
- safe edit/delete/return flow review

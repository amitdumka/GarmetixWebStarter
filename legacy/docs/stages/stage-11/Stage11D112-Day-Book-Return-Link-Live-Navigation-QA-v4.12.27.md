# Stage 11D-112 — Day Book Return Link + Live Navigation QA

Version: v4.12.27  
Build: GARMETIX-11D112-20260630-4227  
Date: 2026-06-30

## Purpose

Fix practical Day Book navigation after opening a source transaction page. A user can now open a sale, purchase inward, voucher, cash voucher, vendor payment or journal/accounting source page from Day Book and return to Day Book without losing the earlier date, type, search, page size, page number or journal visibility state.

## User-facing changes

- Day Book source links now append `fromDayBook=1` before navigating away.
- Source pages show a **Back to Day Book** button only when opened from Day Book and a saved Day Book session state exists.
- The Back button returns to `/day-book?restore=1`; the Day Book page restores the saved sessionStorage state.
- The Day Book **New** quick-create actions also carry the same return hint:
  - Sale Invoice
  - Purchase Inward
  - Vendor Payment
  - Vendor Advance
  - Book Voucher
  - Cash Voucher

## Pages covered

- `/billing`
- `/billing/new`
- `/purchase`
- `/purchase/new`
- `/vouchers`
- `/cash-vouchers`
- `/vendor-payments`
- `/accounting`

## Files changed

- `frontend/garmetix-web/pages/day-book/index.vue`
- `frontend/garmetix-web/components/UiDayBookReturnButton.vue`
- `frontend/garmetix-web/pages/billing/index.vue`
- `frontend/garmetix-web/pages/billing/new.vue`
- `frontend/garmetix-web/pages/purchase/index.vue`
- `frontend/garmetix-web/pages/purchase/new.vue`
- `frontend/garmetix-web/pages/vouchers/index.vue`
- `frontend/garmetix-web/pages/cash-vouchers/index.vue`
- `frontend/garmetix-web/pages/vendor-payments/index.vue`
- `frontend/garmetix-web/pages/accounting/index.vue`
- app version metadata and current validation scripts

## Manual QA

1. Open `/day-book`.
2. Select an older book date.
3. Apply a type filter and search text.
4. Open a Sale/Purchase/Voucher/Cash Voucher/Vendor Payment/Journal source page.
5. Confirm the source page shows **Back to Day Book**.
6. Click **Back to Day Book**.
7. Confirm the same Day Book date, type, search, page size, page and journal visibility are restored.

## Validation

- Stage 11D-112 validation checks version metadata, source-link return hints, the reusable return button, and covered source pages.
- Current release validation should include Stage 11D-112 plus app-version safety, route-access, navigation-menu coverage and secret hygiene.

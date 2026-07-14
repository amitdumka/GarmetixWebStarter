# Stage 11D-111 — Day Book State Memory + Detail Drawer Polish

Version: v4.12.26  
Build: GARMETIX-11D111-20260630-4226  
Date: 2026-06-30

## Purpose

Fix practical Day Book usage issues found during live review:

1. Day Book should not reset to today's date after opening a sale, purchase, voucher, cash voucher, or payment source page and returning.
2. Day Book detail drawer should be wide enough for real transaction review.
3. Day Book detail drawer should show user-relevant tabulated fields, including the transaction ID, instead of developer/raw object data.

## Changes

- Added session-level Day Book state memory using `sessionStorage`.
- Preserves:
  - book date / date preset
  - type filter
  - journal visibility checkbox
  - search text
  - page number
  - page size
  - month/year/custom range filters
- Saves Day Book state before source navigation and quick-create navigation.
- Restores the saved state on returning to `/day-book` during the same browser session.
- Widened Day Book detail slideover to `sm:max-w-4xl`, `lg:max-w-6xl`, and `xl:max-w-7xl`.
- Added visible transaction `ID` in the drawer header and summary card.
- Filtered detail key/value rows to relevant fields only.
- Removed internal/noisy fields such as company/store IDs, audit fields, row versions, and developer JSON fields from user-facing detail tables.
- Improved Day Book detail API responses so sale, purchase, voucher, cash voucher, customer receipt, vendor payment, and journal details return concise user-facing detail objects.
- Added `LineType` to related sale/purchase/payment lines so the detail drawer can show meaningful grouped rows.

## Validation

Run:

```bash
python3 scripts/validation/current-release-checks.py
```

Expected result:

- Stage 11D-111 validation passed
- App version string safety passed
- Frontend route access audit passed
- Navigation menu coverage passed
- Secret hygiene passed

## Live test checklist

- Open `/day-book`.
- Choose an older date and search/type filter.
- Open a Sale/Voucher/Purchase source page.
- Return to `/day-book`.
- Confirm previous date/search/type/page are restored.
- Click Day Book `Open` on multiple rows.
- Confirm drawer is wider.
- Confirm ID is visible.
- Confirm detail tables show relevant values, not raw JSON or full developer objects.

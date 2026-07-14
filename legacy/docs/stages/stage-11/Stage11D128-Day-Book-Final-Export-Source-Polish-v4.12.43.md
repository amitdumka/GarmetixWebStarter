# Stage 11D-128 - Day Book Final Export + Source Opening Polish - v4.12.43

## Purpose

This stage closes the Day Book evidence workflow after purchase import, Vyapar sale import, payroll and Accounting/GST validation. It gives the accountant/owner a practical export and print path for the exact filtered Day Book view, while keeping existing accounting and posting logic untouched.

## Added backend endpoints

### `GET /api/day-book/export.csv`

Exports the selected Day Book scope as an Excel-compatible CSV evidence file.

Supported query parameters are the same as the Day Book list endpoint:

- `datePreset`
- `date`
- `from`
- `to`
- `month`
- `year`
- `type`
- `q`
- `includeJournal`

The CSV includes:

- Generated timestamp
- Date range
- Filter type
- Search term
- Exported row count
- Total matching row count
- Summary debit/credit/net totals
- Sales, purchase, voucher, payment and journal counts
- Transaction rows with IDs and source paths

### `GET /api/day-book/print`

Returns a print-friendly HTML Day Book evidence view with a **Print / Save PDF** button. This keeps PDF generation browser-native and avoids changing the existing server PDF stack.

## Frontend changes

Page upgraded:

- `frontend/garmetix-web/pages/day-book/index.vue`

Added:

- Final export/source-opening card
- `Export CSV` button
- `Print / PDF` button
- Register header quick CSV/Print actions
- Source-opening hints with:
  - `fromDayBook=1`
  - `dayBookSourceId`
  - `dayBookSourceType`
  - `dayBookDate`
  - `dayBookPreset`
  - `dayBookType`
  - anchor hash for the opened source

## Operator behavior

1. Select Day Book date or period.
2. Select transaction type.
3. Search if required.
4. Toggle journal rows only when accountant requires journal evidence.
5. Export CSV for Excel/accountant review.
6. Open Print/PDF for daily evidence file.
7. Open any source row and return back to Day Book without losing selected filter state.

## Limits / safety

- Export is capped at 5,000 rows per request for safe browser/runtime usage.
- CSV is evidence only. It does not post or repair accounting entries.
- Print view is browser-native HTML print/save-PDF, not a binary server PDF.
- Journal rows remain hidden by default to keep owner day-book view clean.

## Version

- Version: `4.12.43`
- Stage: `Stage 11D-128 Day Book Final Export Source Polish`
- Build code: `GARMETIX-11D128-20260701-4243`

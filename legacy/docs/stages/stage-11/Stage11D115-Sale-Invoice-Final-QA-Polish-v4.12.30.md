# Stage 11D-115 — Sale Invoice Final QA Polish

Version: v4.12.30  
Build: GARMETIX-11D115-20260630-4230  
Date: 2026-06-30

## Purpose

Finish the practical Sale Invoice QA polish after Day Book, Vendor Payment and Purchase Inward source-navigation stages.

## Included changes

- Sale invoice register now remembers browser-session state:
  - date preset
  - custom from/to dates
  - month/year
  - status filter
  - search text
  - page number
  - page size
- Opening a Sale Invoice or Customer Receipt from Day Book now opens the receipt and focuses the sale register on the invoice date instead of leaving the list reset to Today.
- Sale register links preserve `fromDayBook=1` when creating or revising an invoice from a Day Book return context.
- New Sales Invoice page keeps its Invoice Register back link Day-Book-aware.
- Last saved sale invoice alert now has an Open in Register action.
- Sale receipt review modal is wider for store-counter use.
- Sale receipt review now shows a clean key/value invoice summary including invoice ID.
- Receipt split-payment rows now show payment ID, date, mode, amount, reference and status/source.
- Backend receipt payment DTO now includes payment row ID so support/debugging does not require raw JSON.

## Live QA checklist

1. Open `/billing`, set an old custom sale date, status and search, then move to another page and return.
2. Confirm the sale register restores the same filters/page.
3. Open a Sale Invoice row from `/day-book`.
4. Confirm `/billing?invoiceId=...&fromDayBook=1` opens the receipt and focuses the register date/search on that invoice.
5. Open a Customer Receipt row from `/day-book`.
6. Confirm the receipt opens and split-payment details are readable.
7. From `/billing?fromDayBook=1`, click New Invoice and confirm `/billing/new?fromDayBook=1` opens.
8. Save a test invoice and use Open in Register to return to the exact invoice.
9. Confirm A4/A5/thermal print buttons still work.

## Validation

```bash
python3 scripts/validation/current-release-checks.py
```

Expected:

```text
Stage 11D-115 validation passed
Current release validation passed for Stage 11D-115 Sale Invoice Final QA Polish / v4.12.30.
```

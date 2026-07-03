# Day Book Final Export + Source Opening Polish - v4.12.43

## Summary

Day Book now has a final evidence export layer for accountant and owner review.

## What changed

- CSV export endpoint: `GET /api/day-book/export.csv`
- Print/PDF-friendly endpoint: `GET /api/day-book/print`
- Day Book page now shows a final export/source-opening card.
- CSV export includes summary totals, counts, transaction IDs and source paths.
- Print view can be saved as PDF from the browser.
- Source links now carry stronger Day Book return context and source anchors.

## Why this matters

After import and GST/accounting validation, the store needs one daily/monthly evidence file that can be shared with the accountant. This stage makes Day Book usable as that operational evidence register.

## Test checklist

1. Open **Day Book**.
2. Select one date and apply.
3. Export CSV and confirm it opens in Excel.
4. Click Print/PDF and confirm a print view opens.
5. Save as PDF from browser print.
6. Open Sale/Purchase/Voucher/Vendor Payment source from a row.
7. Return to Day Book and confirm date/type/search/page are remembered.
8. Toggle journal rows and confirm export includes them only when enabled or Journal type is selected.

## Known limitations

- Print/PDF is browser print, not server PDF rendering.
- Export is capped at 5,000 rows.
- It does not replace Accounting/GST validation or financial-year closeout.

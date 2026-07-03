# Garmetix v4.11.84 — Purchase Import Parser QA

Stage 11D-69 improves supplier invoice import recognition after real invoice testing showed vendor/header/accounting/footer rows could still become item draft rows.

## Main changes

- Parser now works table-first and only reads rows after a detected item table header.
- Fallback parsing is conservative: no item table header means only high-confidence standalone rows with item text plus validated quantity/rate/amount are accepted.
- Wrapped product names are merged with the next numeric row before parsing.
- Header/vendor/payment/bank/tax-summary/footer rows are ignored earlier.
- Each upload/reparse stores `parser-line-decisions.json` so support can see which OCR lines were detected or ignored and why.
- Draft line review messages now show why an item row was detected and what still needs review.
- Import review UI shows OCR confidence per line and adds an “Ignore low-confidence” action.

## User workflow

1. Upload supplier invoice.
2. Open Stored proof / audit files and review `ParserLineDecisions` when wrong lines are detected.
3. Mark wrong rows as Ignore.
4. Save draft so vendor learning remembers the correction.
5. Upload another invoice from the same vendor to confirm repeated mistakes are skipped.

## Notes

This stage does not change Digital Bill CRM. It keeps purchase transaction retry hotfix, OCR/parser, vendor learning, proof retention, duplicate override, and barcode generation from previous packages.

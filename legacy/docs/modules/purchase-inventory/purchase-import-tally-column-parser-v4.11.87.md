# v4.11.87 Purchase Import Tally Column Parser

This stage improves supplier invoice recognition for common Tally Prime style invoices and vendor-specific column layouts.

## What changed

- Added a multi-line item table header detector for OCR text where headers are split across many lines.
- Added serial-number row grouping so wrapped item descriptions and article names are joined before parsing.
- Added a Tally/garment column parser for rows containing HSN, quantity, rate, gross amount, discount, GST rate and taxable amount.
- Added S.K APPARELS / S.S SETH SAAB style product naming: Brand + Art No + Size.
- Improved invoice number extraction so `Tax Invoice` and `Original for Recipient` are not treated as invoice numbers.
- Improved date extraction for Indian `dd-MM-yyyy` invoice dates.
- Parser line diagnostics continue to explain why each row was detected or ignored.

## Expected result for the SKA/2415/2025-26 sample

The parser should extract:

- Invoice number: `SKA/2415/2025-26`
- Date: `13-10-2025`
- Vendor GSTIN: `19ACJPA9582C1ZI`
- Vendor: `S.K APPARELS`
- Item lines: 35
- Taxable amount: approximately `175584.70`
- Grand total: `194354.00`

Each item line should use the garment article/model as the product name instead of repeating only the vendor/brand text.

## Notes

The parser is still draft-first. OCR output must be reviewed before posting to purchase inward.

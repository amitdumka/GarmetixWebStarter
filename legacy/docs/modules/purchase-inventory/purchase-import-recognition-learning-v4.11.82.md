# Purchase Import Recognition Learning v4.11.82

Version: 4.11.82  
Stage: Stage 11D-67 Purchase Import Recognition Learning

## Purpose

This hot stage improves supplier invoice OCR recognition after real-world testing showed that header rows, vendor/accounting details, and footer totals could be wrongly created as invoice items.

## Changes

- Added item-table-first parsing. The parser now tries to locate the real item table header before creating draft product lines.
- Added stronger header/footer/accounting filters for GSTIN, invoice header, buyer/seller blocks, bank details, GST summaries, totals, declarations, IRN/e-way bill, and signature sections.
- Added line amount plausibility checks so footer/tax-summary numeric rows are less likely to be treated as product rows.
- Added vendor-wise learning profile table: `PurchaseInvoiceImportVendorProfiles`.
- When users mark wrong rows as ignored or map/correct products, the system stores learned ignored-line signatures and product aliases by vendor GSTIN/vendor.
- On future imports from the same vendor, learned ignored rows are skipped and learned product aliases are reused.

## Important workflow

OCR still creates a draft only. User verification remains required before stock, vendor balance, accounting, and GST are posted.

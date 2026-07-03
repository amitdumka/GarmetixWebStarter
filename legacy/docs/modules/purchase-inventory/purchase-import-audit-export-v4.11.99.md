# Purchase Import Audit Export - v4.11.99

Stage: Stage 11D-84 Purchase Import Audit Export
Build: GARMETIX-11D84-20260627-4199

## Purpose

This stage adds export and evidence tools for the supplier invoice import workflow so every scanned/imported supplier bill can be reviewed, audited, shared for debugging, and backed up outside the application UI.

## Backend endpoints

- `GET /api/purchase-import/batches/{id}/audit-json`
  - Downloads a JSON audit package for one import batch.
  - Includes the batch, corrected draft lines, posting report, checklist, warnings, reconciliation and stored file metadata.

- `GET /api/purchase-import/batches/{id}/lines-csv`
  - Downloads a CSV of all import draft lines.
  - Includes ignored rows, match status, product name, barcode, HSN, quantity, MRP, cost, discount, tax and review messages.

- `GET /api/purchase-import/vendor-profiles/export`
  - Downloads vendor invoice learning profiles as JSON.
  - Includes ignored row patterns, product aliases and vendor learning notes.

## Frontend

On `Purchase → Import Acceptance`:

- Each recent import row has:
  - `CSV` to download line review data.
  - `Audit` to download the full JSON evidence package.
- The posting report header has CSV/Audit JSON download buttons for the selected batch.
- The page hero includes `Export learning` for vendor invoice learning backup/review.

## Notes

These exports do not delete or modify import data. Posted supplier invoice proofs remain protected. Use these files when debugging invoice parser issues, validating discounts/taxes, or sending a specific import case for review.

## Next recommended part

`v4.12.00 - Purchase Import Final Parser Templates + Real Invoice QA`

Planned:

1. Add more Tally Prime template variations.
2. Show parser template used for each import.
3. Add per-vendor template override control.
4. Add final real-invoice QA checklist before moving outside purchase import.

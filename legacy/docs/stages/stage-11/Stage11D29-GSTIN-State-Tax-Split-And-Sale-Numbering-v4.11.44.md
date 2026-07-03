# Stage 11D-29 / v4.11.44 - GSTIN State Tax Split and Sale Invoice Numbering

## Changes

- Sale invoice numbers now use `StoreCode/YYYYMM/INV/NNNN`, for example `SM/202606/INV/0001`.
- Purchase inward creation now decides IGST versus CGST/SGST by comparing company GSTIN state code with vendor GSTIN state code.
- Sales invoice creation continues to compare company GSTIN state code with customer GSTIN state code and now stores item tax type as IGST for interstate sales.
- Added `scripts/production/recalculate-gst-split-by-gstin.sh` to recalculate existing sale and purchase invoice CGST/SGST/IGST after vendor/customer GSTIN is corrected.
- Purchase import v8 uses the same GSTIN split logic.

## Rules

- Same GSTIN state code: CGST + SGST.
- Different GSTIN state code: IGST.
- Missing/invalid party GSTIN: defaults to local CGST + SGST until GSTIN is updated and the recalculation script is run.

## Recalculate existing invoices

```bash
cd /opt/garmetix/current
chmod +x scripts/production/recalculate-gst-split-by-gstin.sh
./scripts/production/recalculate-gst-split-by-gstin.sh
```

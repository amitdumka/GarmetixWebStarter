# v4.11.96 - Purchase Import Price Tag Integration

Build: **Stage 11D-81 Purchase Import Price Tag Integration**

This stage connects posted supplier invoice imports and purchase inwards directly with the 50×30 mm / 50×25 mm thermal price-tag printing page.

## What changed

- Posted supplier invoice import drafts now show a **Print tags** action.
- The action opens `/price-tags` with the posted import/purchase inward already loaded.
- Purchase invoice list actions now include **Tags** so labels can be reprinted later.
- Purchase receipt modal now includes **Print price tags**.
- Price Tags page supports route-based loading:
  - `/price-tags?purchaseInwardId=<purchaseInvoiceId>`
  - `/price-tags?importBatchId=<postedImportBatchId>`
- Backend endpoint added:
  - `GET /api/price-tags/import-batches/{id}/items`

## Recommended flow

1. Import supplier invoice.
2. Verify draft lines, split sizes, generate barcodes, and post inward.
3. Click **Print tags**.
4. Choose 50×30 mm or 50×25 mm.
5. Print through USB desktop printer or download TSPL for Bluetooth/mobile printer utilities.

## Safety

Only posted supplier invoice import batches can load tag items from the import-batch endpoint. Unposted drafts remain editable and do not print as final stock labels.

# Stage 12B.2 POS Sale Draft

Version marker: `5.12.2`

Stage 12B.2 starts the real sale invoice extraction in `modular/apps/pos/pages/sale.vue`.

## What Changed

- Added a central modular version marker at `modular/config/version.ts`.
- POS shell now shows the current Stage 12B.2 status.
- POS `/sale` now includes:
  - setup/store loading
  - default salesman loading from `billing/options`
  - bank account loading for non-cash payments
  - barcode/product search from `product-lookup`
  - invoice cart with quantity, MRP, discount, tax and line total
  - bill discount and rounded payable calculation
  - cash and non-cash payment rows
  - local draft persistence
  - `Save & Print` against the existing `billing/sales` API
  - authorized PDF fetch for invoice print opening

## Backend Contract Used

- `GET /api/setup/status`
- `GET /api/stores`
- `GET /api/bank-accounts`
- `GET /api/billing/options`
- `GET /api/product-lookup`
- `GET /api/product-lookup/barcode/{barcode}`
- `POST /api/billing/sales`
- `GET /api/billing/sales/{id}/pdf`

## Current Limits

- Customer profile adjustments, credit notes, loyalty redemption and customer search are not yet moved.
- Print opens the generated PDF in a new browser tab/window after save.
- The legacy billing page remains the fallback until POS parity is complete.

## Next Step

Stage 12B.3 should add customer search/profile adjustments and harden the print queue around recently saved invoices.

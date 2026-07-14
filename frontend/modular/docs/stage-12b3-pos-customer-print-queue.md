# Stage 12B.3 POS Customer And Print Queue

Version marker: `5.12.3`

Stage 12B.3 improves the POS sale workflow with customer profile adjustments and a usable print queue.

## What Changed

- POS `/sale` now supports customer search using the existing billing customer search endpoint.
- Matched customer selection loads profile balances from the existing customer profile endpoint.
- POS `/sale` now supports adjustment payments for:
  - customer store credit
  - credit notes
  - customer advance receipts
  - loyalty point redemption
- Adjustment amounts are added to the payment total and posted as the same payment adjustment rows used by the legacy billing page.
- Saved invoices are stored in a browser local print queue after `Save & Print`.
- POS `/print` now shows:
  - invoices saved from the current browser
  - recent invoices loaded from the server
  - print/reprint actions using the invoice PDF endpoint

## Backend Contract Used

- `GET /api/billing/customers/search`
- `GET /api/billing/customers/{id}/profile`
- `GET /api/billing/sales/recent`
- `GET /api/billing/sales/{id}/pdf`
- `POST /api/billing/sales`

## Current Limits

- GSTIN live validation is still not moved into POS.
- The print queue is local browser storage plus recent server invoices; it is not yet a server-side durable queue.
- Sale returns remain in their prepared route for a later extraction stage.

## Next Step

Stage 12B.4 should harden POS sale operator flow: keyboard shortcuts, barcode focus behavior, customer GSTIN validation, and route guards for login-required pages.

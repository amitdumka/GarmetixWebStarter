# Stage 12B.5 - POS Returns Foundation

Version: 5.12.5

## Scope

This stage keeps the work inside `modular/apps/pos` and reuses the existing ASP.NET Core billing API. It does not change the legacy app, backend endpoints, or database schema.

## Added

- POS sales return page connected to existing billing endpoints:
  - `GET /api/billing/sales/recent?take=100`
  - `GET /api/billing/sales/{invoiceId}/receipt`
  - `POST /api/billing/sales/{invoiceId}/returns`
  - `GET /api/billing/sales/{returnInvoiceId}/pdf`
- Invoice number, QR, customer, and mobile search.
- Returnable invoice list with item loading.
- Return item quantity entry with sold quantity clamping.
- Refund amount, refund mode, bank account, and reason fields.
- Save & Print Return action that creates the return/credit note and opens the return PDF.
- Local print queue handoff for created return invoices.
- Counter shortcuts:
  - `F2` focuses invoice search.
  - `F4` saves the return.
  - `Esc` clears the search.

## How To Test

1. Run the API and POS app.
2. Login to POS.
3. Open `/returns`.
4. Search or scan an existing invoice number.
5. Click Return, enter a return quantity, and save.
6. Confirm a credit note/return invoice is created and the PDF opens.
7. Open `/print` and confirm the created return document appears in the local queue.

## Remaining Notes

- The page uses the existing sales invoice PDF endpoint for return note printing because sales returns are stored as return invoices.
- The next POS slice should harden held bills or day-open/day-close depending on counter priority.

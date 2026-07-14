# Sales Exchange Notes

This update completes the Exchange item flow.

## Endpoint

`POST /api/billing/sales/{id}/exchange`

The exchange flow:

1. Loads the original invoice.
2. Returns selected original invoice item quantities.
3. Creates a linked return invoice / credit note.
4. Reverses sold stock only for returned quantities.
5. Adds returned value to customer store credit when not refunded.
6. Creates a new replacement sales invoice.
7. Applies available store credit against the replacement bill.
8. Collects any additional payment through the selected payment mode.

The replacement invoice uses `OriginalInvoiceId` to retain traceability back to the original sale.

## Frontend

Billing page now has an **Exchange** action beside eligible invoices. The modal lets the operator select returned quantities, add replacement products, and collect extra amount if replacement value is higher.

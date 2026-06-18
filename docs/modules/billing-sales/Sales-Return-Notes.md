# Sales Return / Credit Note Notes

This update adds selected-item sales return support without linking to GST return auto-generation yet.

## Endpoint

```http
POST /api/billing/sales/{id}/returns
```

Payload:

```json
{
  "refundAmount": 0,
  "refundPaymentMode": null,
  "bankAccountId": null,
  "reason": "Size return",
  "items": [
    { "invoiceItemId": "...", "quantity": 1 }
  ]
}
```

## Behavior

- Blocks returns against cancelled invoices and return invoices.
- Validates return quantities against remaining returnable quantity.
- Creates a linked return invoice / credit note using `ReturnInvoice = true` and `InvoiceType.Return`.
- Sets `OriginalInvoiceId` on the credit note.
- Reverses sold stock only for selected returned items.
- Reduces customer billed amount.
- Adds store credit balance when refund is not paid immediately.
- Supports optional cash/bank refund and posts accounting lines for credit note/refund.
- Billing page now has a **Return** action with selected-item quantity entry.

## Still pending

- Full exchange workflow remains separate and should be added after deciding how store credit should be applied to a replacement invoice in accounting.

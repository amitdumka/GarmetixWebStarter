# Purchase Vendor Payment Voucher Notes

This update adds vendor payment voucher creation for unpaid/partially paid purchase invoices.

## Endpoint

```http
POST /api/purchase/invoices/{id}/payment-voucher
```

Payload:

```json
{
  "amount": 1000,
  "paymentMode": 0,
  "bankAccountId": null,
  "paymentDetails": "Paid from counter",
  "slipNumber": "PV-001",
  "remarks": "Supplier balance payment"
}
```

## Behavior

- Blocks payment for cancelled purchase invoices.
- Caps payment amount to the outstanding balance.
- Requires a bank account for non-cash payment modes.
- Creates a regular `Voucher` with `VoucherType.Payment`.
- Posts accounting journal lines against the vendor party ledger and cash/bank settlement ledger.
- Updates vendor paid amount and purchase invoice status.
- Purchase page now shows a **Pay** action for invoices with balance.

## Notes

This intentionally creates an accounting voucher rather than silently changing the purchase invoice, so later audit/reporting can trace supplier payments.

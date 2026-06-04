# Billing POS

The starter includes a focused POS endpoint:

```http
POST /api/billing/sales
```

It also includes receipt/history endpoints:

```http
GET /api/billing/sales/recent
GET /api/billing/sales/{id}/receipt
POST /api/billing/sales/{id}/cancel
```

It performs one transaction:

- Creates or reuses a customer by mobile number.
- Generates a daily invoice number such as `S-20260604-0001`.
- Validates stock for each item.
- Calculates taxable amount, tax amount, total discount, bill amount, paid amount, and balance.
- Saves `Invoice`, `InvoiceItem`, and `InvoicePayment`.
- Updates stock `SoldQty` and `SoldValue`.
- Returns receipt-ready data for invoice print/share views.
- Cancels invoices and reverses sold quantity/value once.

Example body:

```json
{
  "companyId": "00000000-0000-0000-0000-000000000000",
  "storeGroupId": "00000000-0000-0000-0000-000000000000",
  "storeId": "00000000-0000-0000-0000-000000000000",
  "customerName": "Walk-in Customer",
  "customerMobileNumber": "",
  "paymentMode": 0,
  "paidAmount": 1000,
  "billDiscountAmount": 0,
  "items": [
    {
      "productId": "00000000-0000-0000-0000-000000000000",
      "barcode": "ABC123",
      "quantity": 1,
      "mrp": 1000,
      "discountAmount": 0
    }
  ]
}
```

Payment mode uses the existing enum integer values:

- `0`: Cash
- `1`: Card
- `2`: UPI
- `7`: Cheque

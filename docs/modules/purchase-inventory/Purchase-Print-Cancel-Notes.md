# Purchase Invoice List, Print, and Cancellation Notes

This update makes Purchase a proper document workflow instead of only an inward-entry form.

## Backend endpoints

- `GET /api/purchase/lookup-options`
  - Returns product categories, subcategories, and taxes for the inward item form.
- `GET /api/purchase/invoices/recent`
  - Returns the recent purchase register with amount, paid amount, balance, status, inward number, and vendor data.
- `GET /api/purchase/invoices/{id}/receipt`
  - Returns a print/view DTO for a selected purchase invoice.
- `GET /api/purchase/invoices/{id}/pdf`
  - Downloads a purchase invoice PDF.
  - Query options:
    - `format=a4|a5|thermal-2|thermal-3`
    - `copy=store|supplier|office|duplicate`
    - `reprint=true|false`
    - `signatures=true|false`
- `POST /api/purchase/invoices/{id}/cancel`
  - Cancels an invoice, reverses inward stock, reduces vendor totals, and posts a reversing purchase journal.

## Frontend

`/purchase` now includes:

- Purchase invoice list.
- View/print/download action per invoice.
- A4/A5/thermal purchase print options.
- Store/supplier/office/duplicate copy labels.
- Reprint/signature toggles.
- Cancel action for users with delete permission.
- Tax/category/subcategory selectors while adding a new inward product.

## Notes

- Cancellation is implemented as a controlled void/cancel operation, not a physical delete.
- Purchase paid amount is inferred from the purchase accounting journal created during inward posting.
- Vendor payment voucher creation is still pending as a separate task. The current purchase payment is already posted to accounting/bank ledgers, but it does not yet create a normal Voucher document row.

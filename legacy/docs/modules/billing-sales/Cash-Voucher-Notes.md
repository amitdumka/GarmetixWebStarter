# Cash Voucher Notes

Cash Vouchers are independent off-book cash records. They do not post to ledger, journal, bank statement, or accounting voucher tables.

## Endpoints

- `GET /api/cash-vouchers` - list company/store scoped cash vouchers.
- `GET /api/cash-vouchers/{id}` - get one cash voucher.
- `GET /api/cash-vouchers/{id}/pdf?format=a4-two|a5-one&reprint=true|false&signatures=true|false` - download professional cash voucher PDF.
- `POST /api/cash-vouchers` - create a cash voucher.
- `PUT /api/cash-vouchers/{id}` - update a cash voucher, requires edit permission.
- `DELETE /api/cash-vouchers/{id}` - delete a cash voucher, requires delete permission.

## Print / PDF behavior

The Cash Voucher page supports:

- A4 with two copies: Office Copy and Recipient Copy.
- A5 single copy.
- Reprint stamp toggle.
- Signature line toggle.
- Browser print.
- Downloadable PDF from the backend.

The PDF uses the same in-project lightweight PDF renderer as regular vouchers, so no new package is required.

# Stage 12E.4 - Books Voucher Review

Version: 5.12.18

## Scope

This stage replaces the modular Books voucher placeholder with a read-only voucher review workspace. It is intentionally review and print-download only.

## Added

- Searchable voucher list for payment, receipt and expense vouchers.
- Voucher type filter that works whether the API serializes enum values as numbers or names.
- Selected voucher detail panel.
- Ledger, party, bank account and employee reference labels.
- Authenticated PDF downloads for:
  - A4 voucher PDF
  - A5 voucher PDF
  - Reprint voucher PDF
- Version marker and modular structure validation coverage.

## Connected Endpoints

- `vouchers`
- `vouchers/{id}`
- `vouchers/{id}/pdf`
- `ledgers`
- `parties`
- `banks`
- `bank-accounts`
- `employees` when permitted by the signed-in user

## Safety

- No POST, PUT, DELETE, ledger posting, voucher conversion, or accounting correction endpoints are called.
- Party-ledger and bank-ledger internal flags remain hidden from the user.
- PDF downloads use the stored bearer token instead of opening an unauthenticated raw URL.

## How To Test

```bash
npm --prefix modular run build:books
```

Run locally:

```bash
npm --workspace @garmetix/books-web --prefix modular run dev
```

Open `http://localhost:3104/vouchers` after logging in with an accounting-capable user.

## Next Step

Stage 12E.5 should connect petty cash review in the Books app: sheet list, daily balance summary, A5 print download readiness, and mismatch-alert visibility without edit/save actions.

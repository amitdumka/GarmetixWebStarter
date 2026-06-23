# Stage 12E.3 - Books Bank Operations

Version: 5.12.17

## Scope

This stage connects bank-operation review screens inside the modular Books app. It is read-only and does not post transactions, reconcile statement lines, update cheque lifecycle, or edit bank records.

## Added

- `Cash Details` now acts as the read-only bank operations workspace.
- Connected endpoints:
  - `banks`
  - `ledgers`
  - `parties`
  - `vendors`
  - `bank-accounts`
  - `accounting/bank-transactions`
  - `accounting/bank-statement/{bankAccountId}`
  - `accounting/bank-reconciliation/{bankAccountId}`
  - `cheque-logs`
  - `vendor-bank-accounts`
  - `bank-account-details`
- Bank account selector with statement and reconciliation summary.
- Read-only tables for bank transactions, cheques, vendor bank accounts, bank account details, and bank account masters.
- Transaction type/mode labels added to Books utilities.

## Safety

- No POST, PUT, DELETE, reconcile, unreconcile, repair, or lifecycle endpoints are called.
- Bank and cheque audit actions stay in later explicit write-action slices.
- Values are presented for review only; hidden internal ledger flags remain hidden.

## How To Test

```bash
npm --prefix modular run build:books
```

Run locally:

```bash
npm --workspace @garmetix/books-web --prefix modular run dev
```

Open `http://localhost:3104/cash-details` after logging in with an accounting-capable user.

## Next Step

Stage 12E.4 should connect vouchers as read-only first: voucher list, ledger/party/bank labels, PDF download link, and print readiness without create/edit/delete actions.

# Stage 14C.3 - Books Bank Reconciliation Parity

Version: 6.0.24

## Scope

This stage promotes the Books `Bank Operations` route from read-only review to guarded write parity for bank settlement work, bank transaction entry and statement reconciliation.

Implemented frontend capabilities:

- Create and edit accounting bank transactions through `/api/accounting/bank-transactions`.
- Delete bank transactions through `/api/accounting/bank-transactions/{id}` with an exact confirmation phrase.
- Reconcile and unreconcile bank statement lines through `/api/accounting/bank-statement-lines/{id}/reconcile` and `/unreconcile`.
- Update cheque lifecycle status through `/api/accounting/cheque-logs/{id}/lifecycle`.
- Keep vendor bank accounts, account access details and bank account masters visible for audit context.

## Guard Rails

- Bank transaction save requires `POST BANK TRANSACTION`.
- Bank transaction delete requires `DELETE BANK TRANSACTION`.
- Statement reconcile requires `RECONCILE BANK LINE`.
- Statement unreconcile requires `UNRECONCILE BANK LINE`.
- Cheque lifecycle update requires `UPDATE CHEQUE STATUS`.

## Notes

- This stage uses existing backend endpoints and does not add a database migration.
- Live mutation remains user-driven from the UI only; readiness scripts do not post or mutate live data.
- Store and company context is resolved from setup status, selected store and selected bank account.

## Validation

- `npm --prefix frontend/modular --workspace @garmetix/books-web run build`
- `npm run modular:books:bank-reconciliation-parity`

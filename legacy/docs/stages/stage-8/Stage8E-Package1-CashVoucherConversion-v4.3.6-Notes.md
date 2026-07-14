# Stage 8E Package 1 - Cash Voucher Conversion Audit

Version: 4.3.6
Build: `GARMETIX-8E-20260616-4360`

## Delivered

- Owner/Admin-only movement from Off Book Cash Voucher to a regular cash accounting voucher.
- Owner/Admin-only movement from eligible cash payment, receipt, or expense vouchers to Off Book.
- Accounting journals and any linked bank/cheque artifacts are removed when a regular voucher moves Off Book.
- Source records are soft-deleted from active registers and retained for audit.
- Immutable conversion history stores both document IDs/numbers, direction, amount, party, particulars, reason, operator, and timestamp.
- Converted documents cannot be edited or deleted; corrections require a new controlled record.
- Cash Vouchers now includes eligible on-book records, conversion actions, and conversion history.

## Validation

- Backend and test projects compile with zero warnings and errors.
- 64 non-PostgreSQL tests pass.
- Nuxt production build passes.
- The migration is limited to the new `CashVoucherConversions` table and its indexes.

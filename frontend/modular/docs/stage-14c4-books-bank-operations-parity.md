# Stage 14C.4 - Books Bank Operations Parity

Version: 6.0.42

## Scope

This stage completes the next Books banking slice on the existing `Cash Details / Bank Operations` route instead of adding a duplicate banking page.

Implemented frontend capabilities:

- Vendor bank account guarded edit flow through `/api/vendor-bank-accounts/{id}`.
- Secure bank detail review/update through `/api/bank-account-details/{id}`.
- Sensitive bank detail fields stay masked until the user types `REVEAL BANK DETAIL`.
- Vendor bank edit requires `UPDATE VENDOR BANK`.
- Bank detail update requires `UPDATE BANK DETAIL`.
- Account numbers are masked in review tables.
- Settlement Closure Dashboard reads `/api/bank-reconciliation/settlement-closure`.
- Evidence CSV handoff downloads `/api/bank-reconciliation/settlement-closure/evidence.csv`.
- Statement Import Plan documents the next import/review workflow without inventing an unverified write endpoint.

## Guard Rails

- Existing bank transaction, statement reconciliation and cheque lifecycle gates remain unchanged.
- Vendor bank and bank access detail edits are user-driven only.
- The parity script is non-mutating and checks source markers only.
- No database migration or backend contract change is included in this stage.

## Validation

- `npm run modular:books:bank-operations-parity`
- `npm --prefix frontend/modular run check`
- `npm --prefix frontend/modular --workspace @garmetix/books-web run build`

## Next

Continue Books parity with GST/accounting report finalization, financial lock acceptance, and Books final closure before moving back to Main Back Office/Admin acceptance.

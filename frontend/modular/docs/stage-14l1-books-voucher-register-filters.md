# Stage 14L.1 - Books Voucher Register Filters

## Scope

Frontend-only polish for the modular Books voucher register. The shared ASP.NET API, PostgreSQL schema, deployment scripts and live SRP host were not changed.

## Added

- Month/year filter using the voucher date's `YYYY-MM` value.
- From Date and To Date range filters.
- Voucher Type filter using stable voucher enum values while still supporting string labels.
- Ledger Type filter derived from loaded ledger metadata such as ledger group/account type fields.
- Exact Ledger filter using the existing searchable `USelectMenu` pattern.
- Text search now includes ledger type along with voucher number, slip number, party, particulars, remarks, ledger and payment mode.
- Clear Filters action visible only when a list filter is active.

## Notes

- Ledger type/group filtering is client-side because the voucher page already loads vouchers and ledgers together.
- If a ledger has no readable group/type metadata, it appears as `Unclassified`.
- If a voucher references a missing ledger, it appears under `Unlinked`.
- No pull, commit, push or deploy was performed in this pass because Claude is coordinating Books module git/deploy flow.

## Validation

- `npm --prefix frontend/modular --workspace @garmetix/books-web run build`

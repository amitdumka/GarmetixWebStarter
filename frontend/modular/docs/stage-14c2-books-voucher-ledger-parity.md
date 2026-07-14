# Stage 14C.2 Books Voucher Ledger Parity

Version: 6.0.22

## Scope

Stage 14C.2 promotes the modular Books voucher page from read-only review to guarded voucher entry parity with the legacy accounting flow.

## Added

- Voucher create, edit and delete actions in `apps/books/pages/vouchers.vue`.
- `Save & Print` create flow that saves the voucher, refreshes the list, selects the saved row and downloads the A5 PDF.
- Local-date voucher save handling using `YYYY-MM-DDT00:00:00` to avoid timezone day rollback.
- Visible ledger selection only. Party ledger and party id resolution remain hidden and are handled by the backend posting service.
- Non-cash voucher validation that requires a bank account before save.
- `setup/accounting-defaults` fallback when ledger, party or bank-account prerequisites are missing.
- Accounting master ledger-sync repair with exact confirmation phrase `REPAIR LEDGER SYNC`.
- Ledger statement viewer for posted-entry audit follow-up.
- Books API helper mutation wrappers for `post`, `put` and `del`.

## Internal Ledger Rules

- party-ledger flags are not user-facing controls.
- The modular form sends `partyId: null` and the selected `ledgerId`.
- The backend resolves whether the selected ledger is linked to a party and sets the internal party flags.
- bank-ledger handling remains internal to backend voucher posting.

## Validation

Run:

```bash
npm run modular:books:voucher-ledger-parity
npm run modular:check
npm --prefix frontend/modular run build:books
```

## Deployment Cadence

This stage is checkpoint 1 after the Stage 14B.12 `.127` deployment. It is frontend-focused and does not require a database backup. Deploy on checkpoint 3, or earlier only if a manual live check is requested.

## Next

Stage 14C.3 should complete bank, cheque, bank statement and reconciliation write parity in the Books app.

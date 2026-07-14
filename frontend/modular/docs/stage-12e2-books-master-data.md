# Stage 12E.2 - Books Master Data

Version: 5.12.16

## Scope

This stage connects read-only accounting master data inside the modular Books app. It does not add, edit, delete, post, repair, reconcile, print, or change any accounting records.

## Added

- Read-only `Accounting Masters` page connected to:
  - `ledger-groups`
  - `ledgers`
  - `parties`
  - `banks`
  - `bank-accounts`
  - `accounting/trial-balance`
  - `accounting/ledger-sync/status`
- Read-only `Parties` page connected to `parties` and `ledgers`.
- `BooksMasterTable` for compact, scrollable accounting tables.
- Books utility helpers for Indian dates, enum labels, and accounting option labels.
- Ledger and bank account internal flags remain hidden; the UI only shows link health as `Linked` or `Missing`.

## Safety

- No write endpoints are called.
- Ledger sync repair remains disabled in this stage.
- Party-ledger and bank-ledger creation remains server-owned.

## How To Test

```bash
npm --prefix modular run build:books
```

Run locally:

```bash
npm --workspace @garmetix/books-web --prefix modular run dev
```

Open `http://localhost:3104/accounting` and `http://localhost:3104/parties` after logging in with an accounting-capable user.

## Next Step

Stage 12E.3 should connect bank operations as read-only first: bank transactions, bank statement, reconciliation summary, cheque logs, vendor bank accounts, and bank account details.

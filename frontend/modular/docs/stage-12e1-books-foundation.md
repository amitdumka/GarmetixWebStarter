# Stage 12E.1 - Books Foundation

Version: 5.12.15

## Scope

This stage turns the placeholder Books app into a protected modular accounting shell. It does not change the backend API, database schema, accounting posting, voucher logic, GST logic, or legacy pages.

## Added

- Books app shell with app switcher, API/auth/status cards, logout, and accounting-focused left navigation.
- Books auth route guard and login page using the shared auth/token storage.
- Books dashboard foundation with read-only summary loading through the shared API client.
- Route coverage pages for:
  - Accounting
  - Parties
  - Vouchers
  - Petty Cash
  - Cash Details
  - Vendor Payments
  - Vendor Settlements
  - Debit Notes
  - Credit Notes
  - Commercial Notes
  - GST Returns
  - GST Reports
  - GST Production Readiness
  - Financial Year Locks
- Shared `BooksPlaceholder` component for routes that should be visible before write-sensitive accounting actions are enabled.
- Books utility API helper for token-aware read requests.

## Safety

- Accounting write actions are not enabled in this stage.
- Party ledger, bank ledger, voucher posting and GST export behaviors remain in the legacy app until each endpoint contract is verified.
- The route shell is protected by the same token storage used by other modular apps.

## How To Test

```bash
npm --prefix modular run build:books
```

Then run the Books app locally:

```bash
npm --workspace @garmetix/books-web --prefix modular run dev
```

Open `http://localhost:3104`.

## Next Step

Stage 12E.2 should connect the Books accounting master data: ledger groups, ledgers, parties, and bank accounts as read-only lists before enabling add/edit actions.

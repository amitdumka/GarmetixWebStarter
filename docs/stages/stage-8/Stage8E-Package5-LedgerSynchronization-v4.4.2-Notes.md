# Stage 8E Package 5 - Ledger Synchronization Hardening (v4.4.2)

Build code: `GARMETIX-8E-20260617-4420`

This package completes the next Stage 8E accounting hardening item after financial-year locking and journal validation.

## Backend

- Added ledger synchronization status and repair endpoints under Accounting.
- Party ledger validation now detects empty ledger links, missing ledgers, wrong-company ledgers, non-party ledgers, wrong party ledger type, duplicate/shared party-ledger links, and party name drift.
- Bank-account ledger validation now detects empty ledger links, missing ledgers, wrong-company ledgers, wrong ledger types, duplicate/shared bank-ledger links, party-flagged bank ledgers, and bank ledger name drift.
- Party and bank-account save flows now ignore unsafe existing ledger links and create/reuse only a dedicated safe ledger.

## Frontend

- Added Accounting → Ledger Sync tab.
- Added ledger-sync issue metric, issue table, severity, message, fix action, and one-click repair action.

## Deployment

- Keeps the safe Mac mini deployment default `RESET_DATABASE_ON_DEPLOY=false`.
- No schema changes were required for this package.

# Stage 8A Package 12 - v4.0.10

## Scope

Standardize the remaining Cash Voucher page while preserving its required separation from regular vouchers and the accounting system.

## Changes

- Replaced the plain register card with the shared retryable register panel.
- Retained a useful page-level error after failed loads instead of showing an empty register.
- Added Payment, Receipt, Expense, and All voucher-type filtering.
- Preserved search, responsive actions, metrics, print controls, and the wide entry workspace.
- Replaced UTC-derived form dates and voucher-date stamps with local calendar helpers.
- Kept Cash Voucher records independent from ledgers, journals, bank transactions, and regular vouchers.
- Migrated the persistent UI audit baseline and marked `/cash-vouchers` reviewed.
- Extended frontend Message Logs to capture browser console errors and warnings.

## Next

Rework Non-GST Goods as the next Off Book package, including its multi-item entry workspace, retained report errors, and server PDF printing.

## Version

- Version: `4.0.10`
- Build code: `GARMETIX-8A-20260614-4010`

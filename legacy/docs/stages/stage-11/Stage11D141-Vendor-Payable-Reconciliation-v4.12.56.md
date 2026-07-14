# Stage 11D-141 — Vendor Payable / Purchase Settlement Reconciliation — v4.12.56

## Purpose

This stage adds a final closeout/reconciliation layer for supplier payable balances after the purchase import, purchase inward, vendor payment, purchase return and debit-note settlement work.

The goal is to make vendor payable issues visible before month close. This stage does not auto-repair balances and does not change purchase posting logic.

## Added

### Backend

- `GET /api/purchase/vendor-payable-reconciliation`
- `GET /api/purchase/vendor-payable-reconciliation/evidence.csv`

### Frontend

- New page: **Purchase → Vendor Payable Reco**
- Route: `/purchase/vendor-payable-reconciliation`
- Shortcut from **Vendor Payments** page.

## Checks covered

- Vendor master `BillAmount` vs active purchase invoice total.
- Vendor master `Paid` vs active purchase payment rows.
- Vendor master balance vs invoice outstanding minus open debit notes.
- Purchase invoice status vs active payment rows.
- Paid invoice with remaining balance.
- Overpaid purchase invoice.
- Purchase invoice item-row evidence.
- Purchase invoice accounting journal evidence.
- Non-cash vendor payments without bank/POS/UPI mapping.
- Vendor payment voucher existence, amount and mode consistency.
- Vendor payment voucher journal evidence.
- Open debit notes, over-adjusted debit notes and printed/shared debit-note evidence.
- Debit-note settlement total vs adjusted amount.

## Result status

The report shows:

- `Complete` when no critical or warning issue exists.
- `Not Complete` when any blocker/warning remains.

## CSV evidence

CSV includes:

- Status and date range.
- Summary metrics.
- Payment/settlement source summary.
- Critical/warning issues.
- Vendor-wise evidence.
- Closeout checklist.

## Operator rule

Do not manually edit vendor master balances to hide differences. Correct through purchase invoice, vendor payment edit/delete, vendor settlements, voucher/accounting repair or controlled data consistency repair.

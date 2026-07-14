# Stage 8I Package 10 - Store Day / Petty Cash Runtime Hotfix (v4.9.9)

## Fixed

### Store Day frontend runtime error

Error:

```text
Store day opening failed
y.success is not a function
```

Cause: newer pages called `feedback.success(...)`, while `useUiFeedback()` only had `notify`, `saved`, `updated`, etc.

Fix:

- Added `success()` alias in `useUiFeedback`.
- Store Day page now uses notify-compatible success handling.

### Petty cash calculation

Petty cash book calculation now includes both:

- Voucher receipts/payments/expenses
- Cash Voucher receipts/payments/expenses

This fixes missing receipt/expense values when they were entered through Cash Voucher flow.

### Petty cash print

Petty cash PDF now prints:

1. A5 summary page.
2. A5 transaction detail page.

The detail page includes income/expense/adjustment lines from:

- Cash sales
- Due receipts
- Voucher receipts/payments/expenses
- Cash Voucher receipts/payments/expenses
- Bank withdrawal/deposit
- Customer due / non-cash adjustments

### Day close correction

Added APIs:

- `POST /api/store-day/reopen`
- `POST /api/store-day/delete-close`

The Store Day page now has:

- Reopen Day
- Delete Close

These void the current close, generated closing cash detail and generated petty cash sheet so the day can be corrected and closed again.

### One record per day rules

Added guard:

- One attendance per employee per day.
- One petty cash sheet per store per day.

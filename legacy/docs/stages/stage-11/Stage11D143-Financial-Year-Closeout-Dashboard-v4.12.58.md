# Stage 11D-143 — Financial Year Closeout Dashboard — v4.12.58

## Purpose

This stage adds one consolidated closeout control page for the selected financial period. It does not post, repair, delete, undo or lock anything by itself. It gathers evidence and shows whether the period is ready for owner/accountant sign-off before Financial Year Lock is created or verified.

## Backend

New endpoints:

- `GET /api/financial-year-closeout`
- `GET /api/financial-year-closeout/evidence.csv`

The endpoint checks:

- Sales invoice item total, GST snapshot, payment row and journal evidence.
- Purchase inward item total, GST snapshot, payment row and journal evidence.
- Journal line balance.
- Negative stock rows and stock cost value.
- Customer dues and customer credit-note balance.
- Vendor payable and vendor debit-note balance.
- Goods-return credit expiry and open/unprinted credit notes.
- Payroll close-month readiness and outstanding salary payment.
- Purchase import correction/acceptance evidence.
- Vyapar imported sale invoice presence.
- Matching active Financial Year Lock covering the selected period.

## Frontend

New page:

- **Accounting → FY Closeout**
- Route: `/financial-year-closeout`

The page shows:

- Complete / Not Complete status.
- Critical and warning issue count.
- Metric cards for sales, returns, purchases, dues, payables, GST, stock and payroll.
- Closeout section matrix linking to detailed modules.
- Blocking/warning issue table.
- FY lock evidence table.
- Final closeout checklist.
- Operator rules.
- Known limitations.
- Next module candidates.
- CSV evidence export.

## Status rule

The dashboard is **Complete** only when there are no critical issues and no warnings. A full matching FY lock is required for completion.

## Operator rule

Do not hard-delete financial evidence after close. Use controlled reversal, revision, return, commercial note adjustment or approved unlock workflow.

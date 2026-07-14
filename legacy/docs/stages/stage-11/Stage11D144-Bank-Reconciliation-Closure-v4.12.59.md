# Stage 11D-144 — Bank Reconciliation Closure — v4.12.59

## Purpose

This stage adds a final Bank Reconciliation / Payment Settlement Closure layer for the owner/accountant before month-end or financial-year lock.

It is a validation and evidence layer. It does not auto-create bank transactions, auto-import statements, or change old sale/purchase/payment posting logic.

## Added

- `GET /api/bank-reconciliation/settlement-closure`
- `GET /api/bank-reconciliation/settlement-closure/evidence.csv`
- Frontend route: `/bank-reconciliation-closure`
- Menu: Accounting → Bank Reco Closure
- Access-control route registration
- CSV evidence export

## What it validates

- Non-cash sale receipt settlement evidence.
- Non-cash purchase/vendor payment settlement evidence.
- Customer advance receipt bank proof.
- Voucher receipt/payment/expense bank proof.
- Salary payment journal/manual bank evidence.
- Missing bank account mapping.
- Missing UTR/slip/gateway/reference number.
- Unmatched settlement rows with no matching bank transaction or statement line.
- Bank transactions without accounting journal.
- Unreconciled bank transactions.
- Unmatched bank statement lines.

## Status rule

Status is **Complete** only when there are no Critical issues.

Warnings should still be reviewed and accepted by the owner/accountant before period closeout.

## Operator rule

Do not lock a period while UPI/Card/NEFT/RTGS/IMPS/Cheque payments are missing bank account mapping or bank proof.

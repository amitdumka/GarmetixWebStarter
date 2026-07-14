# Bank Reconciliation / Payment Settlement Closure — v4.12.59

## Location

Accounting → Bank Reco Closure

Route:

```text
/bank-reconciliation-closure
```

## Backend APIs

```http
GET /api/bank-reconciliation/settlement-closure
GET /api/bank-reconciliation/settlement-closure/evidence.csv
```

## Evidence sections

- Settlement closure status
- Critical/warning issue count
- Payment mode reconciliation
- Bank account evidence
- Blocking/warning issues
- Settlement evidence rows
- Final closeout checklist
- Operator rules
- Known limitations
- Next module candidates

## Critical blockers

- Non-cash payment row without bank account mapping.
- Bank transaction without accounting journal.

## Warnings

- Missing UTR/slip/gateway/reference number.
- Settlement row not matched to a bank transaction or statement line.
- Bank transaction not reconciled.
- Statement line not matched to bank transaction.
- Salary payment needs manual bank proof/journal evidence.

## Limitations

- This stage does not import bank statement files.
- Multiple gateway receipts settled as one bank credit may require manual review.
- SalaryPayment currently has no BankAccountId, so salary bank evidence is checked through journal/manual proof.

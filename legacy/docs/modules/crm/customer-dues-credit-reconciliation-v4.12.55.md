# Customer Dues / Credit Balance Reconciliation — v4.12.55

This module gives the owner/accountant a period-wise customer receivable and store-credit validation layer.

## User-facing page

- Route: `/customers/dues-reconciliation`
- Menu: **CRM → Customer Dues Reco**
- Customer register shortcut: **Dues Reco**

## API

- `GET /api/customers/dues-reconciliation`
- `GET /api/customers/dues-reconciliation/evidence.csv`

Supported filters:

- `companyId`
- `storeGroupId`
- `storeId`
- `from`
- `to`

## Safety rules

- Do not manually overwrite `Customer.CreditBalance` to hide differences.
- Use controlled sale settlement, advance receipt, credit note adjustment, or data consistency repair.
- Non-cash advance receipts must carry bank/POS/UPI mapping before day closing.
- No cash refund workflow is added here; goods return remains exchange/credit-note led as per the public return policy.

## Evidence included

- Final status.
- Critical/warning counts.
- Customer-wise due, advance, credit note and credit balance difference.
- Credit source summary.
- Operator closeout checklist.
- Known limitations.
- CSV export for accountant/owner records.

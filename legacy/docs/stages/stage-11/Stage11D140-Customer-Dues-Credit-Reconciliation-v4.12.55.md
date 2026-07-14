# Stage 11D-140 — Customer Dues / Credit Balance Reconciliation — v4.12.55

Base: v4.12.54 Stage 11D-139 Goods Return Policy Invoice Link.

## Added

- New backend endpoint: `GET /api/customers/dues-reconciliation`.
- New CSV evidence endpoint: `GET /api/customers/dues-reconciliation/evidence.csv`.
- New CRM page: **CRM → Customer Dues Reco** at `/customers/dues-reconciliation`.
- Shortcut button from the Customer register.

## What the reconciliation checks

- Customer invoice due amount for active sale invoices.
- Invoice `PaidAmount` vs active `InvoicePayments` rows.
- Invoices marked `Paid` while balance remains.
- Overpaid invoices.
- Customer master `CreditBalance`.
- Open customer advance receipts.
- Open customer credit notes from sale returns / goods-return policy handling.
- Credit balance mismatch between customer master and open advance/credit-note sources.
- Over-adjusted advance receipts.
- Advance `AvailableAmount` mismatch.
- Over-adjusted credit notes.
- Open credit notes that are not marked printed/shared.
- Non-cash advance receipts without bank/POS/UPI mapping.

## Closeout behavior

The module shows **Complete** only when there are no Critical or Warning issues for the selected date range and workspace filter.

The page is intentionally a validation/evidence layer. It does not auto-repair balances because receivable and credit corrections must remain audit-safe.

## Test checklist

1. Build and deploy with `docker compose up --build`.
2. Open **CRM → Customer Dues Reco**.
3. Select the target date range and run reconciliation.
4. Confirm metrics, credit source summary, issue table and customer-wise evidence load.
5. Export CSV evidence and open it in Excel.
6. Test one due invoice, one customer advance receipt, one credit note from sales return, and one non-cash advance without bank mapping.
7. Correct data through controlled sale settlement, advance receipt, credit note adjustment, or data consistency repair.
8. Re-run until status becomes **Complete**.

## Recommended next module

Vendor Payable / Purchase Settlement final reconciliation.

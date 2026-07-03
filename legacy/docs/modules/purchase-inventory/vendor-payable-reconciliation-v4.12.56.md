# Vendor Payable / Purchase Settlement Reconciliation — v4.12.56

## Location

**Purchase → Vendor Payable Reco**

Route:

```text
/purchase/vendor-payable-reconciliation
```

API:

```text
GET /api/purchase/vendor-payable-reconciliation
GET /api/purchase/vendor-payable-reconciliation/evidence.csv
```

## Business use

Use this page before closing purchase/vendor payable for the month. It gives owner/accountant evidence for supplier balances, vendor advances, debit notes and vendor payment vouchers.

## What to review first

1. Critical issues.
2. Vendor master bill/paid mismatches.
3. Invoice status/payment mismatches.
4. Open debit notes.
5. Non-cash vendor payments missing bank mapping.
6. Voucher/journal evidence warnings.

## Correction path

- Use **Purchase** to correct invoice metadata or controlled replacement/cancellation.
- Use **Vendor Payments** to edit/delete wrong payment rows.
- Use **Vendor Settlements** to adjust debit notes against outstanding purchase invoices or supplier refund.
- Use **Accounting / Data Consistency Repair** only for legacy drift.

## Known limitation

Vendor master balances are current master values. For historic close-date checks, compare carefully with the accounting ledger and Day Book evidence.

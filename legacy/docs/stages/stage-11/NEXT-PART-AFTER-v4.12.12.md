# Next Part after v4.12.12

## Stage 11D-98 — Vyapar Live Import QA Fixes

After deploying v4.12.12, test the real Vyapar sale files through:

- Sales → Vyapar Sale Import
- Preview
- bank/POS/UPI mapping
- fully matched filter
- confirm import
- imported invoice list
- batch undo page

Expected validation:

1. Already imported invoices auto-hide.
2. Payment Status Paid + Balance zero never creates false due.
3. Payment shortfall becomes bill discount only when Vyapar says Paid and Balance zero.
4. Due invoices still keep balance as due.
5. Bank/POS/UPI payment rows post to mapped accounts.
6. Customers are matched by mobile number.
7. Batch undo cancels imported invoices and restores stock/accounting.

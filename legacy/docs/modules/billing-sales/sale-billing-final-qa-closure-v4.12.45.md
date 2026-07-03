# Sale/Billing Final QA Closure — v4.12.45

## What changed

The Billing module now has a final QA closure page and API that summarize whether sale invoice operations are ready for production closeout.

## Page

```text
/billing/final-qa
```

## API

```http
GET /api/billing/final-qa
GET /api/billing/final-qa/evidence.csv
```

## Practical tests

1. Create one cash sale invoice.
2. Create one UPI/Card/POS sale invoice with bank account mapping.
3. Create one mixed-payment sale invoice such as cash + UPI.
4. Confirm payment rows equal the invoice paid amount.
5. Confirm non-cash rows have bank/POS/UPI account mapping.
6. Confirm barcode is visible in invoice item evidence.
7. Confirm sale stock-out movement exists.
8. Confirm sale accounting journal exists.
9. Revise one invoice from Invoice Replacements and approve it.
10. Export CSV evidence and save it with billing closeout proof.

## Known limitations

- This is a validation and evidence layer only; it does not repair invoices.
- Bank settlement is not bank-statement reconciliation yet.
- Visual print acceptance remains in Print Final Acceptance.
- Old historical invoices may need Data Consistency repair before passing closure.

# Stage 11D-142 — Goods Return / Exchange Operational Acceptance — v4.12.57

Base: v4.12.56 Stage 11D-141 Vendor Payable Reconciliation.

## Added

- New API endpoint: `GET /api/goods-return/acceptance`.
- New CSV evidence endpoint: `GET /api/goods-return/acceptance/evidence.csv`.
- New frontend page: **Sales → Goods Return Acceptance** at `/goods-return-acceptance`.
- Sidebar/menu link in modern and legacy shells.

## What the page validates

- Sales return is linked to the original invoice.
- Return/exchange is within 7 days of original purchase.
- No refund/payment is posted against goods return under the standard no-refund policy.
- Every sales return has a linked customer credit note.
- Credit-note amount matches return amount.
- Credit note is not over-adjusted.
- Open credit note is printed/shared with customer.
- Open credit note expiry is tracked.
- Replacement exchange invoice is linked to the original sale and consumes the return credit.
- Returned item barcode evidence is present.

## Credit-note expiry policy implemented

- Credit notes issued April to December are tracked as valid until 31 March of the same financial year.
- Credit notes issued in January, February or March are tracked with six-month validity from the note date.
- Open expired credit is a Critical issue.
- Open credit expiring within 30 days is a Warning.

## Important note

This is a validation and evidence layer. It does not change posted return, exchange, credit-note, billing, accounting or stock behavior.

## Test steps

1. Run `docker compose up --build`.
2. Open **Sales → Goods Return Acceptance**.
3. Select date range and click **Run acceptance**.
4. Confirm status shows **Complete** or **Not Complete**.
5. Create/test one sales return within 7 days and confirm credit note appears.
6. Confirm any refund amount is flagged as Critical.
7. Create/test an exchange invoice and confirm replacement linkage appears.
8. Check credit-note expiry bucket.
9. Export CSV evidence and open it in Excel.

# Stage 11D-130 — Sale/Billing Final QA Closure — v4.12.45

## Purpose

Add a final non-mutating Sale/Billing QA closure layer after print acceptance, Day Book export and post-import Accounting/GST validation.

This stage is for production sign-off of live sale invoice behavior, especially:

- cash-only sale evidence
- non-cash sale bank/POS/UPI mapping evidence
- mixed-payment split rows
- invoice replacement/revision pending approval checks
- sale GST item snapshots
- barcode evidence
- accounting journal evidence
- stock-out evidence
- CSV closeout proof

## Backend

New endpoints:

```http
GET /api/billing/final-qa
GET /api/billing/final-qa/evidence.csv
```

The endpoint returns a Complete / Not Complete status for the selected period. It does not mutate data.

Validation checks include:

- sale invoice `PaidAmount` vs `InvoicePayments` row total
- overpaid active invoice detection
- missing sale item rows
- missing barcode on invoice item rows
- zero/negative item quantity
- invoice GST vs item GST snapshot mismatch
- non-cash payment rows missing bank/POS/UPI account mapping
- missing `SalesInvoice` accounting journal
- missing `SalesInvoice` stock-out movement
- mixed-payment invoice without split rows
- multiple payment rows without invoice-level `MixPayments`
- paid status with remaining balance
- cancelled paid invoice without cancellation journal warning
- revised invoice whose original invoice is not yet cancelled/reversed

## Frontend

New page:

```text
/billing/final-qa
```

Menu link added under Sales:

```text
Sales → Billing Final QA
```

The Billing page also exposes a **Final QA** button.

The page shows:

- final status
- critical/warning issue counts
- metric cards
- closure checks
- blocking/warning issues table
- payment mode reconciliation
- GST snapshot by rate
- invoice-wise evidence table
- closeout checklist
- operator rules
- known limitations
- next-module candidates
- CSV evidence export

## Closeout rule

The stage reports **Complete** only when no Critical or Warning issues remain for the selected period.

## Operator rule

Do not hard-delete live sale invoices. Use controlled invoice replacement/revision and approval when wrong GST, price, payment or printed evidence needs correction.

## Validation

Static validation script:

```bash
python3 scripts/validation/stage11d130-sale-billing-final-qa-closure-check.py
```

Full runtime validation must be run on the deployment machine:

```bash
docker compose up --build
```

Then open:

```text
Sales → Billing Final QA
```

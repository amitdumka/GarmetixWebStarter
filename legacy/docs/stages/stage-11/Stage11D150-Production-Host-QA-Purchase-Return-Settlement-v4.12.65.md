# Stage 11D-150 Production Host QA + Purchase Return Advanced Settlement - v4.12.65

## Purpose

This stage closes two remaining production-hardening gaps:

1. Production host build/runtime QA evidence after `docker compose up --build`.
2. Advanced purchase return settlement acceptance for supplier returns, ITC reversal, debit-note adjustment/refund, and accounting audit.

## Backend endpoints

- `GET /api/production-host-build-qa`
- `GET /api/production-host-build-qa/evidence.csv`
- `GET /api/purchase-return/advanced-settlement`
- `GET /api/purchase-return/advanced-settlement/evidence.csv`

## Frontend pages

- Maintenance → Host Build QA: `/production-host-build-qa`
- Purchase → Return Settlement QA: `/purchase-return/advanced-settlement`

## Purchase return advanced checks

- Formal purchase return document number exists.
- Return item rows exist and match header count/quantity/amount.
- Taxable, CGST, SGST, IGST and total GST match item snapshots.
- ITC reversal rows exist for every returned item.
- ITC reversal total and components exactly match the purchase return tax split.
- Purchase-return stock-out quantity equals returned quantity.
- Vendor debit note is linked and sourced from the purchase return.
- Vendor debit note amount/tax matches the return.
- Debit note is not over-adjusted.
- Vendor settlements do not exceed return amount.
- Settlement allocations and debit-note purchase-payment rows match adjusted amount.
- Supplier refund settlements have voucher, bank mapping, reference and journal/bank proof.
- Purchase-return journal exists, is balanced, and credits Input GST equal to the ITC reversal.
- Printed/shared return proof is visible.

## Production host QA checks

Automatic probes:

- Database connectivity.
- Core table query probes.
- Purchase return, ITC reversal and vendor settlement table probes.
- Journal/bank/audit table probes.
- Production environment, CORS and JWT checks.

Manual QA evidence checklist:

- Docker build log.
- Hosted `/api/health` check.
- Login smoke by role.
- Sidebar/page smoke check.
- CSV export smoke check.
- PDF/print smoke check.
- Backup/restore drill.
- Message Logs review.

## Build hardening fix

Fixed duplicate `Printed` parameter in `PurchaseReturnDetailDto`, which could break .NET publish for purchase-return detail loading.

## Version

- Version: `4.12.65`
- Stage: `Stage 11D-150 Production Host QA + Purchase Return Settlement`
- Build code: `GARMETIX-11D150-20260703-4265`

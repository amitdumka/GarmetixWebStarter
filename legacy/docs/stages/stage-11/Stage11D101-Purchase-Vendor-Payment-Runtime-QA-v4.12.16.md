# Stage 11D-101 — Purchase/Vendor Payment Runtime QA

Version: `4.12.16`
Base: `v4.12.15 Stage 11D-100 Purchase Inward Date Vendor Payment Maintenance`

## Purpose

Stage 11D-100 added two important editable flows:

1. user-controlled purchase inward date for new/edit purchase inward, and
2. vendor payment view/edit/delete maintenance.

Stage 11D-101 packages the runtime validation needed before this should be treated as production-stable on live data.

## Added files

```txt
scripts/runtime/stage11d101-purchase-vendor-payment-runtime-validation.sh
scripts/runtime/stage11d101-purchase-vendor-payment-db-check.sql
scripts/validation/stage11d101-purchase-vendor-payment-runtime-qa-check.py
docs/stages/stage-11/Stage11D101-Purchase-Vendor-Payment-Runtime-QA-v4.12.16.md
docs/stages/stage-11/NEXT-PART-AFTER-v4.12.16.md
```

## Runtime checks included

The DB QA script checks:

- `PurchaseInvoices.InwardDate` exists.
- Recent purchase invoices show supplier invoice date vs inward date.
- Purchase stock movements with `SourceType = PurchaseInvoice` are checked against `PurchaseInvoices.InwardDate`.
- Recent vendor payments show linked purchase invoice, vendor, voucher and deletion status.
- Purchase invoice status is compared with active payment row totals.
- Deleted vendor payments are checked for still-active linked voucher, bank transaction or accounting journal.
- Non-cash active vendor payments without bank account are listed for correction.
- Recent daily vendor payment totals are summarized.

## Run after deploy

```bash
python3 scripts/validation/stage11d101-purchase-vendor-payment-runtime-qa-check.py
./scripts/runtime/stage11d101-purchase-vendor-payment-runtime-validation.sh --smoke /opt/garmetix/current
```

For full rebuild and validation:

```bash
./scripts/runtime/stage11d101-purchase-vendor-payment-runtime-validation.sh --build /opt/garmetix/current
```

DB-only check:

```bash
./scripts/runtime/stage11d101-purchase-vendor-payment-runtime-validation.sh --db-only /opt/garmetix/current
```

## Manual QA checklist

1. Create a purchase inward with an older inward date.
2. Confirm the generated inward number month uses selected inward date.
3. Confirm stock movement date equals inward date.
4. Edit purchase inward header/date and verify list/receipt display.
5. Create vendor payment against a purchase invoice.
6. Edit vendor payment date, amount, mode, bank, reference and remarks.
7. Confirm invoice status changes to pending/partially paid/paid correctly.
8. Delete wrong vendor payment and verify linked voucher/accounting/bank rows are no longer active.
9. Create non-cash vendor payment and verify bank account is mandatory.

## Next part

Stage 11D-102 — Purchase/Vendor Payment Live QA Fixes.

Use this after running the above checks on live data and collecting any actual errors.

# Stage 11D-91 — Vyapar Sale Import Runtime Validation

Version: **v4.12.06**  
Base: **v4.12.05 Stage 11D-90 Vyapar Sale Import Review Enhancements**

## Purpose

This stage does not change core sale import posting logic. It adds runtime validation and deployment checks for the Vyapar Sale Import flow so the live database can be verified safely before and after importing historical Vyapar sales.

## Added files

```text
scripts/runtime/stage11d91-vyapar-sale-import-runtime-validation.sh
scripts/runtime/stage11d91-vyapar-sale-import-db-check.sql
scripts/runtime/stage11d91-vyapar-preview-curl-template.sh
scripts/validation/stage11d91-vyapar-sale-import-runtime-validation-check.py
docs/stages/stage-11/Stage11D91-Vyapar-Sale-Import-Runtime-Validation-v4.12.06.md
```

## Runtime checks included

The DB check reports:

1. `SalesInvoices.Remarks` migration/column status.
2. Vyapar imported invoice totals.
3. Duplicate Vyapar source invoice/date mapping.
4. Imported invoices without items.
5. Invoice `PaidAmount` mismatch against `InvoicePayments` rows.
6. Non-cash imported payments without bank/reference/payment details.
7. Imported invoice item count vs stock movement count.
8. Imported invoices grouped by date.

## Run commands

Static package validation:

```bash
python3 scripts/validation/stage11d91-vyapar-sale-import-runtime-validation-check.py
```

Smoke + DB validation on deployed server:

```bash
./scripts/runtime/stage11d91-vyapar-sale-import-runtime-validation.sh --smoke /opt/garmetix/current
```

Full clean rebuild + validation:

```bash
./scripts/runtime/stage11d91-vyapar-sale-import-runtime-validation.sh --build /opt/garmetix/current
```

DB-only validation:

```bash
./scripts/runtime/stage11d91-vyapar-sale-import-runtime-validation.sh --db-only /opt/garmetix/current
```

## Manual UI validation checklist

- Open **Sales → Vyapar Sale Import**.
- Upload `SaleReport_01_08_25_to_31_03_26_FIN YEAR_2025_2026.xlsx` and preview.
- Upload `SaleReport_01_04_26_to_30_06_26_ Apr_june_2026.xlsx` and preview.
- Confirm fully matched invoices are visible in the **Fully matched** filter.
- Confirm missing barcode/product rows appear in review.
- Confirm every non-cash Bank/POS/UPI source can be mapped to the right bank account.
- Import a small fully matched test set first.
- Re-preview the same file and confirm already imported same-source/date invoices are hidden.
- Open **Sales → Imported Vyapar Sales** and verify source invoice mapping.
- Run DB validation again.

## Next part

**Stage 11D-92 — Vyapar Import Batch Undo + Bulk Barcode Mapping Upload**

Planned:

- Import batch ID/history table or metadata.
- Undo/reverse complete Vyapar import batch.
- Upload filled barcode mapping Excel/CSV and apply mappings in preview.
- Export detailed mismatch workbook from the page.
- Approval confirmation screen before importing historical batches.

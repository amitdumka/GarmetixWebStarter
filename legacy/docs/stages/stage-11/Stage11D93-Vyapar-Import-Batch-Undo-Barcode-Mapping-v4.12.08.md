# Stage 11D-93 — Vyapar Import Batch Undo + Barcode Mapping

Version: **v4.12.08**
Build code: **GARMETIX-11D93-20260629-4208**
Base: **v4.12.07 Stage 11D-92 Vyapar Sale Import Build/Layout Fix**

## Goal

Make Vyapar Sale Import safer for repeated and large historical sale imports.

## Implemented

### Backend

- Confirmed Vyapar imports now create a batch id and batch reference.
- Each imported invoice remark stores:
  - `VyaparSaleImport`
  - `VyaparImportBatchId=<guid>`
  - `VyaparImportBatchRef=<ref>`
  - `VyaparSourceInvoice=<source invoice>`
  - `VyaparInvoiceDate=<date>`
- New batch audit entries are written to `AuditLogEntries` with:
  - `Module = SaleImport`
  - `EntityName = VyaparSaleImportBatch`
  - `Source = VyaparSaleImport`
- Confirm import now requires explicit final approval from frontend.
- New APIs:

```txt
POST /api/sale-import/vyapar/barcode-mapping/preview
GET  /api/sale-import/vyapar/batches
POST /api/sale-import/vyapar/batches/{batchId}/undo
```

### Frontend

- Sale import page now has a final approval dialog before database posting.
- Sale import page can upload filled barcode mapping Excel/CSV and apply mappings to preview rows.
- Added page:

```txt
/billing/vyapar-import-batches
```

- Added menu item:

```txt
Sales → Vyapar Import Batches
```

- Imported Vyapar Sales page links to the batch page.

### Batch undo behavior

Admin/Delete-permission users can undo a Vyapar import batch. Undo does **not hard-delete** invoices. It safely:

- cancels active imported invoices in the selected batch
- posts stock return movements
- reverses sale accounting through existing accounting cancellation service
- clears paid amount/payment mode on cancelled invoices
- updates customer bill count/amount
- writes audit evidence

## Notes

- No new database table is added in this stage.
- This uses existing `SalesInvoices.Remarks` and `AuditLogEntries` for batch tracking.
- Older Vyapar imports before v4.12.08 may not appear in batch history unless they already have `VyaparImportBatchId` in remarks.

## Validation

Run:

```bash
python3 scripts/validation/stage11d93-vyapar-import-batch-undo-barcode-mapping-check.py
```

Then deploy/build:

```bash
docker compose build --no-cache
docker compose up -d
```

## Next part

**Stage 11D-94 — Vyapar Sale Import Runtime Fixes after Live Test**

Expected work:

- deploy v4.12.08
- upload both Vyapar sale files
- test mapping upload
- import fully matched invoices
- verify batch list
- test batch undo on a small test batch
- fix any build/runtime issue found from live logs

# Stage 11D-98 — Vyapar Import History Clear Fix

Version: `4.12.13`
Build code: `GARMETIX-11D98-20260629-4213`

## Problem fixed

When a Vyapar sale import was undone/cancelled, the sale invoices kept `VyaparSaleImport` markers inside `SalesInvoices.Remarks`. On the next upload of the same Vyapar file, preview treated those cancelled records as already imported and auto-hidden them.

## Backend changes

- Preview no longer auto-hides cancelled Vyapar imported invoices.
- Confirm no longer blocks against cancelled Vyapar imported invoices.
- Added admin endpoint:

```http
POST /api/sale-import/vyapar/history/clear
```

This clears stale Vyapar import markers from invoice remarks without deleting sale invoices. It is intended after batch undo/cancel/hard-delete workflows when the same Vyapar file must be previewed again.

## Frontend changes

- `Sales → Vyapar Import Batches` now has `Clear Cancelled History`.
- The clear dialog defaults to cancelled/undone records only.
- Active invoice history can be cleared only after explicitly enabling the dangerous option.
- `Sales → Vyapar Sale Import` now links to `Import Batches / Clear History`.

## Safety

- Clear history does not delete invoices.
- Clear history does not reverse stock/accounting.
- Normal recommended use is after batch undo or cancelled imported invoices.
- Audit log entries are written with source `VyaparSaleImportHistoryClear`.

## Next part

Stage 11D-99 — Vyapar Import Live QA Fixes after deploy and real-file preview/import testing.

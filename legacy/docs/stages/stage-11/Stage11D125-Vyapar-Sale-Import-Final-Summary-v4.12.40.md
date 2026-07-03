# Stage 11D-125 - Vyapar Sale Import Final Summary + Reconciliation Closure - v4.12.40

## Purpose

This stage closes the Vyapar Sale Import workflow from the application side by adding a final summary and reconciliation layer on top of the existing preview, confirm, imported-invoice list, batch undo, barcode mapping and clear-history workflow.

## New endpoint

`GET /api/sale-import/vyapar/final-summary`

Query parameters:

- `companyId` - required
- `storeId` - optional
- `from` - optional date filter
- `to` - optional date filter

The endpoint returns a closure status and reconciliation payload for imported Vyapar sale invoices in the selected range.

## What the final summary checks

- Active, cancelled and total imported invoices.
- Distinct import batches and source invoices.
- Bill total vs item-line total.
- Paid amount vs invoice payment rows.
- Payment mode totals, including non-cash rows without bank mapping.
- GST summary by tax rate.
- Stock-out coverage for imported sale invoices.
- Historical stock bridge movement count and quantity.
- Missing batch markers.
- Duplicate source invoice/date combinations.

## UI changes

Page: **Billing → Vyapar Import Batches**

Added a **Vyapar Sale Import final summary + reconciliation** card with:

- Complete / Not Complete status.
- Active/cancelled invoice count.
- Batch/source invoice count.
- Bill, item, paid and balance totals.
- GST tax and taxable totals.
- Stock-out and bridge quantity totals.
- Payment reconciliation table.
- GST reconciliation table.
- Issue/notice list.
- Closeout checklist.
- Known limitations.
- Recommended next outside-module candidates.
- CSV export for accountant/operator reconciliation.

## Closure status rule

The status is **Complete** only when:

- At least one active imported invoice exists in the selected range.
- There are no error issues.
- There are no warning issues.

If warnings exist, the status is **Not Complete** so the operator can review the issue list before sign-off.

## Known limitations shown in app

- The final summary reconciles only data already imported into Garmetix.
- The original uploaded Excel is not stored by this summary endpoint after browser upload.
- Batch undo cancels/reverses invoices and preserves audit history; it does not hard-delete.
- Historical stock bridge rows need review before trusting item-wise profit.
- Manual clearing of active import history can weaken future auto-hide/duplicate checks.
- Accountant review is still required for GST, bank receipts, stock valuation and day closing totals.

## Test checklist

1. Run `docker compose up --build`.
2. Open **Billing → Vyapar Import Batches**.
3. Confirm the final summary card loads for the default current-month range.
4. Change date range and click **Apply**.
5. Confirm totals and issue list refresh.
6. Click **Export CSV** and verify the file contains summary, payment, GST and issue rows.
7. Compare bill, payment and GST totals with the original Vyapar report for the same range.
8. Confirm a wrong batch can still be reversed using the existing Undo action.

## Version

- Version: `4.12.40`
- Stage: `Stage 11D-125 Vyapar Sale Import Final Summary`
- Build code: `GARMETIX-11D125-20260701-4240`

# Stage 11D-127 - Accounting/GST Post-Import Live Validation - v4.12.42

## Goal

This stage adds a safe validation/evidence layer after the import-heavy stages. It is designed to help the owner/accountant verify that Vyapar sale import, purchase import, accounting, GST, stock, payment rows and month closeout evidence agree before closing a period.

This stage does **not** change sale posting, purchase posting, GST posting, stock ledger posting or payment posting logic.

## New backend endpoints

### `GET /api/post-import-validation/accounting-gst`

Query parameters:

- `companyId` optional
- `storeId` optional
- `from` optional date, defaults to current month start
- `to` optional date, defaults to current month end

Returns:

- `status`: `Complete` or `Not Complete`
- metric cards
- check rows
- issue rows
- GST tax-rate rows
- payment-mode rows
- closeout checklist
- known limitations
- next-module handoff candidates

### `GET /api/post-import-validation/accounting-gst.csv`

Downloads CSV evidence for the same selected period.

## Validation areas

The report validates:

1. Sale invoice bill total vs sale item line total.
2. Sale output GST vs sale item GST snapshots.
3. Sale paid amount vs customer receipt/payment rows.
4. Sale invoice accounting journal existence.
5. Sale stock-out quantity vs sale item quantity.
6. Purchase bill total vs purchase item lines plus freight/round-off.
7. Purchase input GST vs purchase item GST snapshots.
8. Purchase vendor payment evidence rows.
9. Purchase accounting journal existence.
10. Purchase stock-in quantity vs purchase item quantity.
11. Posted purchase-import acceptance Pass status.
12. Purchase-import CorrectionRequired queue.
13. Negative stock rows after import/posting.

## New frontend page

Added page:

- `/accounting-gst-validation`

Menu:

- GST → Accounting/GST Validation

The page includes:

- date range filters
- Complete / Not Complete status
- critical and warning counts
- metric cards
- validation checks table
- issue panel
- GST snapshot by input/output tax rate
- payment reconciliation by direction/payment mode
- CSV evidence export
- closeout checklist
- known limitations
- next-module candidates

## Operator workflow

1. Select the period after running Vyapar sale import and purchase import.
2. Click **Run validation**.
3. Fix Critical issues first.
4. Review Warning rows before closeout.
5. Compare GST rows with GST Reports.
6. Compare imported sale totals with the original Vyapar report.
7. Compare purchase imports with posted supplier proof.
8. Export CSV evidence.
9. Attach the CSV to month-end closeout records.
10. Move to Day Book / Print / Billing / Purchase live QA depending on issue priority.

## Known limitations

- This is a validation/evidence layer only.
- It does not auto-repair accounting, GST, stock, payment rows or dues.
- Purchase invoice paid totals are not persisted on `PurchaseInvoice`; vendor payment rows and vendor reconciliation modules remain the source for payable validation.
- Old data imported before journal/stock posting features existed may need separate controlled repair.
- GST totals are book-based snapshots and still require CA/accountant review before statutory filing.

## Version

- Version: `4.12.42`
- Stage: `Stage 11D-127 Accounting GST Post-Import Validation`
- Build code: `GARMETIX-11D127-20260701-4242`

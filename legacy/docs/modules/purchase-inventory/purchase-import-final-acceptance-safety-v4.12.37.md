# v4.12.37 - Purchase Import Final Acceptance Safety

## Purpose

This stage completes the first practical acceptance layer for Supplier Purchase Invoice Import before moving to work outside the purchase-import module.

## Added

- Real-invoice acceptance status per import batch:
  - Untested
  - Pass
  - Fail
  - NeedsRetest
  - Skipped
- Acceptance notes, tester name and tested-at timestamp.
- Correction safety flags for posted imports.
- Correction-required workflow that does not delete posted proof and does not directly reverse stock/accounting.
- Acceptance and correction counters on Purchase → Import Acceptance.
- Recent import table columns for acceptance status and correction status.
- Schema repair for all new audit columns.

## Correction safety rule

Posted supplier invoice imports are protected audit records. If an import was posted wrongly, the app now allows marking it as CorrectionRequired, but direct undo is intentionally disabled. Correction must be handled by a controlled purchase inward revision/return/reversal flow so stock ledger, vendor balance and accounting stay consistent.

## New API endpoints

```text
POST /api/purchase-import/batches/{id}/acceptance
GET  /api/purchase-import/batches/{id}/correction-safety
POST /api/purchase-import/batches/{id}/request-correction
```

## QA checklist

1. Upload a real supplier invoice.
2. Parse, review and post it.
3. Open Purchase → Import Acceptance.
4. Mark the batch as Pass.
5. Mark another test batch as NeedsRetest.
6. Mark one posted batch as Correction Required and confirm it remains protected.
7. Confirm acceptance/correction counts update.
8. Confirm proof still opens from purchase invoice list/receipt.

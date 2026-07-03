# v4.11.95 - Purchase Import Storage Dashboard

Build: **Stage 11D-80 Purchase Import Storage Dashboard**

## Purpose

Supplier invoice import now stores original PDFs/images, OCR text, parser diagnostics, draft snapshots, and final posted proof snapshots. This stage adds visibility into how much import history and proof data is stored, so failed/unposted scans can be cleaned without touching posted purchase proof.

## Added

### Backend

- New endpoint:

```http
GET /api/purchase-import/storage-summary
```

Returns:

- total import batches
- posted proof batches
- unposted batches
- failed/rejected batches
- total import lines
- total stored file references
- total file bytes
- posted proof bytes
- unposted file bytes
- status-wise breakdown with batch/line/file counts and bill amount

### Frontend

On **Purchase → Import Supplier Invoice**, a new **Import storage** card shows:

- total stored import files
- total import storage used
- protected posted proof storage
- unposted proof/OCR storage
- status-wise draft count, line count, and file size

The existing **Clean failed** action remains protected: it removes only failed/rejected unposted import history by default and does not delete posted purchase proofs.

## Test

1. Upload a supplier invoice and create a draft.
2. Open **Purchase → Import Supplier Invoice**.
3. Check the **Import storage** card.
4. Reject or fail a draft, then click **Clean failed**.
5. Confirm unposted failed/rejected files are cleaned.
6. Post a supplier invoice import.
7. Confirm posted proof bytes remain and proof opens from purchase invoice list/receipt.

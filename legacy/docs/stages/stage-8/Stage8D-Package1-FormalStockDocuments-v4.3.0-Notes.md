# Stage 8D Package 1 - Formal Stock Operation Documents

Version: 4.3.0
Build: `GARMETIX-8D-20260615-4300`
Date: 2026-06-15

## Delivered

- Added formal Stock Operation Document and Stock Operation Item records.
- Preserved product, barcode, HSN, unit, source and destination store, quantity, cost, MRP, and before/after snapshots.
- Linked adjustment, transfer, and physical-count movement rows to their formal document.
- Recorded a verified physical-count document even when no quantity movement is required.
- Added server-owned `StoreCode/YYYYMM/ADJ|ST|PHY/series` numbering.
- Added migration backfill for legacy stock-operation movement rows.
- Added runtime schema repair for installations where automatic migration is disabled.
- Added stable QR tokens and document-number lookup through Document Scanner.
- Added a searchable, filterable stock-document register and wide item-level audit detail.
- Added focused quantity-policy and QR-token tests.

## Next

Stage 8D Package 2 makes the movement ledger the authoritative stock source and introduces documented weighted-average valuation.

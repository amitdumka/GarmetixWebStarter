# Stage 8D Package 2 - Movement Ledger and Weighted Average Valuation

Version: 4.3.1
Build: `GARMETIX-8D-20260615-4310`
Date: 2026-06-15

## Delivered

- Made regular Stock Movement rows the authoritative source for stock quantity and inventory cost.
- Added deterministic weighted-average replay with quantity, average-cost, inventory-value, and cost-impact snapshots on each movement.
- Kept Stock purchase quantity, sold quantity, and cost price as a rebuilt operational projection.
- Routed billing, sale cancellation, sales return/exchange, purchase inward/return/cancellation, product opening, imports, adjustments, transfers, and physical counts through the ledger service.
- Added missing sale-cancellation, billing-import, and purchase-import movement records.
- Preserved complete separation of Off Book Non-GST stock.
- Prevented Product Master edits from overwriting ledger-derived valuation.
- Added a stock valuation and projection-reconciliation register to Stock Operations.
- Extended movement history with running balance, average cost, and inventory value.
- Enhanced Data Consistency repair to rebuild quantity and weighted-average cost together.
- Added migration backfill for existing regular movement history and focused weighted-average unit tests.
- Converted legacy nonzero stock projections without movement history into auditable opening movements.
- Replays later running balances when a historically dated movement is imported or posted.

## Valuation Rule

Incoming stock adds quantity at its document unit cost. Outgoing stock consumes inventory at the current weighted-average cost. The remaining average cost changes only after an incoming movement and becomes zero when quantity reaches zero.

## Next

Stage 8D Package 3 posts accounting entries for shortage, excess, write-off, and transfer operations.

# Stage 8D Package 4 - Stock Intelligence Reports

Version: 4.3.3
Build: `GARMETIX-8D-20260615-4330`

## Delivered

- Restores Status above Notifications in the sidebar footer as two full-width vertical actions.
- Adds the dedicated `/stock-reports` Inventory workspace and permission-aware navigation.
- Uses the regular movement ledger as the quantity and weighted-average valuation source.
- Reports fixed receipt-age indicators: 0-30, 31-60, 61-90, 91-180, and 180+ days.
- Separately identifies stock with no inward history and stock that is out of stock.
- Supports a configurable low-stock threshold with Critical, Low, Watch, and Healthy risk bands.
- Compares ledger quantity and average cost with the operational Stock projection.
- Shows pending stock-operation accounting documents alongside reconciliation mismatches.
- Provides product, barcode, store, risk, age, and reconciliation filtering plus CSV export.
- Preserves Off Book stock separation by excluding `IsOFB` stock.

## Ageing Interpretation

The current report is a receipt-age indicator based on the most recent inward movement for each product/store stock row. It is not FIFO lot ageing because the authoritative valuation method remains weighted average and no inventory-lot layers are persisted.

## Next

Stage 8D Package 5 adds sequence and stock-concurrency tests and formalizes the negative-stock policy.

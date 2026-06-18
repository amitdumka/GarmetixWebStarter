# Stage 8D Package 3 - Stock Operation Accounting

Version: 4.3.2
Build: `GARMETIX-8D-20260615-4320`

## Delivered

- Posts balanced journals for stock excess, shortage, write-off, and inter-store transfer operations.
- Values every journal from the authoritative weighted-average movement posting.
- Creates internal store-wise `Inventory - StoreCode` ledgers under Stock-in-Hand.
- Credits Stock Adjustment Gain under Indirect Income for excess stock.
- Debits Stock Shortage or Stock Write-off under Indirect Expenses for losses.
- Transfers value between source and destination inventory ledgers without recognizing income or expense.
- Stores accounting status and the linked journal entry on each formal stock-operation document.
- Adds a dedicated Write-off tab, `StoreCode/YYYYMM/WO/series` numbering, movement audit, document filter, and journal visibility.
- Uses `Not Required` for zero-value or no-change operations instead of creating empty journals.
- Places Notifications as the single full-width sidebar footer action and keeps service Status in the top bar.

## Next

Stage 8D Package 4 adds stock ageing, low-stock risk, valuation, and stock-reconciliation reports.

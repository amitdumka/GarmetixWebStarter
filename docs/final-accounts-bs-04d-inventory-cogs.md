# Final Accounts BS-04D Inventory And COGS

## Valuation Method

Final Accounts uses the existing perpetual weighted-average inventory evidence for BS-04D. The source of truth is `StockMovements` with `CostImpact`, `CostPrice`, `AverageCostBefore`, `AverageCostAfter`, and `ValuationMethod = "WeightedAverage"`.

This matches current profit/loss and closeout logic, which already calculates COGS from linked sale stock-out movements.

## Posting Policy

- Sale stock-out: debit COGS and credit inventory stock.
- Sale return stock-in: debit inventory stock and credit COGS.
- Purchase stock-in: debit inventory stock and credit the temporary direct purchase account from BS-04C.
- Purchase return stock-out: debit purchase return and credit inventory stock.
- Stock excess: debit inventory stock and credit stock excess.
- Stock shortage/write-off: debit stock shortage and credit inventory stock.
- Inter-store transfer: route source and destination values through inventory transfer clearing.

## Guardrails

- No closing-stock journal is generated in BS-04D. Movement postings are perpetual; adding a separate closing-stock effect would double count inventory.
- Negative stock movement evidence blocks preview.
- Missing cost evidence blocks preview.
- FIFO is not enabled in BS-04D because the current source tables do not store cost layers. The adapter hashes preserve the movement valuation method so FIFO can be added later if layer data is introduced.

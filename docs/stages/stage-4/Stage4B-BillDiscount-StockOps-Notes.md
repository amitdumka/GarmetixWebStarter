# Garmetix Stage 4B — Bill Discount GST Allocation + Stock Operations

## Base package

Built from `Garmetix-Stage4A-SequenceStockConcurrency-v0.9.zip`.

## Goal

Stage 4B closes two production gaps from the earlier audit:

1. Sale bill-level discount was subtracted from invoice total but not reallocated into item taxable/tax amounts.
2. Inventory had a stock movement ledger foundation, but no UI/API workflow for manual stock adjustment, inter-store transfer, or physical stock count.

## Backend changes

### Billing GST allocation

Updated `backend/Garmetix.Api/Billing/BillingEndpoints.cs`.

- Added validation that bill discount cannot be negative.
- Added validation that total discount cannot exceed gross MRP.
- Added `ApplyBillDiscountToInvoiceItems(...)`.
- Bill-level discount is now proportionally allocated across invoice item lines by their inclusive amount.
- After allocation, each affected item updates:
  - `DiscountAmount`
  - `BasePrice`
  - `TaxAmount`
  - `CGSTAmount`
  - `SGSTAmount`
  - `IGSTAmount`
  - `Amount`
- Invoice totals are recalculated from the adjusted item taxable/tax totals.
- `RoundOff` continues to hold the difference between rounded bill amount and taxable + tax amount.

### Stock operation API

Added:

- `backend/Garmetix.Api/Inventory/StockOperationDtos.cs`
- `backend/Garmetix.Api/Inventory/StockOperationEndpoints.cs`

New endpoints:

- `GET /api/inventory/stock-operations/options`
- `GET /api/inventory/stock-operations/movements?take=50`
- `POST /api/inventory/stock-operations/adjustment`
- `POST /api/inventory/stock-operations/transfer`
- `POST /api/inventory/stock-operations/physical-count`

### Stock operation behavior

Manual adjustment:

- Increase stock -> increments `Stock.PurchaseQty`
- Decrease stock -> increments `Stock.SoldQty`
- Posts `StockMovement` as `StockAdjustmentIn` or `StockAdjustmentOut`
- Prevents reducing more than current stock

Physical stock count:

- Compares counted quantity with `Stock.CurrentStock`
- Positive difference -> increments `PurchaseQty`
- Negative difference -> increments `SoldQty`
- Posts `StockMovement` as `PhysicalCountGain` or `PhysicalCountLoss`
- No movement is posted when counted quantity equals system quantity

Stock transfer:

- Source store -> increments `SoldQty`
- Destination store -> increments `PurchaseQty`
- Creates destination stock row if product/barcode does not exist in destination store
- Posts two movement rows:
  - `StockTransferOut`
  - `StockTransferIn`
- Blocks same-store transfer
- Blocks transfer to another company
- Blocks transfer quantity greater than available stock

### Numbering

Updated `backend/Garmetix.Api/Numbering/DocumentNumberService.cs`.

Added sequence-safe document number types:

- `ADJ-yyyyMMdd-0001` for stock adjustment
- `ST-yyyyMMdd-0001` for stock transfer
- `PHY-yyyyMMdd-0001` for physical count

These reuse the Stage 4A `DocumentSequences` table and PostgreSQL transaction advisory lock logic.

### Program registration

Updated `backend/Garmetix.Api/Program.cs`.

Added:

```csharp
app.MapInventoryStockOperationEndpoints();
```

## Frontend changes

Added page:

- `frontend/garmetix-web/pages/stock-operations/index.vue`

Updated navigation:

- `frontend/garmetix-web/components/AppShell.vue`
- Added `Stock Ops` menu item under Operations.

Stock Ops page includes:

- Adjustment tab
- Transfer tab
- Physical Count tab
- Recent movement ledger table
- Current stock indicators
- Store and stock dropdowns

## Limitations / next improvements

- Stock operations are movement-ledger based and do not yet create separate formal adjustment/transfer/count document tables.
- Transfer accounting/valuation is not yet posted to ledgers; this stage updates inventory quantities and stock movement audit only.
- Decrease adjustment and transfer-out are represented by increasing `SoldQty`, consistent with the current cumulative stock model.
- Long-term model improvement should split stock balances into movement-derived quantities instead of relying on cumulative `PurchaseQty - SoldQty`.

# Stock Operation Product Autocomplete - v4.12.69

Stock Operations now supports type-to-search product picking in all stock-changing tabs.

## Where to test

Open **Inventory / Stock Operations**.

## Test checklist

1. Open Adjustment tab and type a product name or barcode in Stock item.
2. Select a result and confirm barcode/store/stock/MRP hint appears.
3. Open Transfer tab and type product/barcode in Source stock.
4. Confirm destination store cannot be the same as source store.
5. Open Physical Count and search product/barcode.
6. Confirm Use System Qty fills the selected stock quantity.
7. Open Write-off and search damaged/unusable product by name/barcode/store.
8. Post one operation only after confirming the selected barcode/store hint.

## Operator note

For barcode-heavy stores, staff should scan or type barcode directly into the autocomplete search instead of scrolling a dropdown.

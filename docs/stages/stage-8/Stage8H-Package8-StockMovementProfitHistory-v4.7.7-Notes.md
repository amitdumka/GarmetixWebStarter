# Stage 8H Package 8 - Stock Movement Profit History (v4.7.7)

This package adds product-wise stock movement history with purchase, sale, purchase-return, sale-return and stock-value impact.

## Included

- New API: `GET /api/inventory/stock-reports/movement-history`.
- Stock Reports page now has a **History** action for every product/stock row.
- Movement history shows purchase inward, sale, sale return, purchase return, opening and stock-operation movements.
- Sale rows calculate sales amount, weighted-average cost of goods sold and profit/loss.
- Sale-return rows reverse the original margin impact.
- Current stock quantity, weighted average cost and current stock value are shown at the top of the movement history view.
- Movement history can be exported to CSV.

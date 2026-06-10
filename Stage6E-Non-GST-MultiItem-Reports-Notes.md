# Stage 6E - Non-GST Multi-Item Memo, Print and Reports

Package: Garmetix-Stage6E-NonGstMultiItemReports-v2.4.zip
Version: 2.4.0
Stage: Stage 6E
Build code: GARMETIX-6E-20260610-240

## Purpose

This stage enhances the Non-GST / Out-of-Scope Goods module into a practical purchase memo and sale cash memo workflow.

The module remains fully audited and visible in books. It is separate from GST invoices and GST reports because these transactions carry 0 GST / out-of-scope tax treatment.

## Implemented

### Backend

- Enhanced `NonGstGoodsItem` with item-level snapshots:
  - `GrossAmount`
  - `TaxableAmount`
  - `TaxRate`
  - `TaxAmount`
  - `CostRate`
  - `CostAmount`
- Added migration:
  - `20260610125000_EnhanceNonGstGoodsMemoReports.cs`
- Updated runtime schema repair to add the same columns for old Docker volumes.
- Added print endpoint:
  - `GET /api/non-gst-goods/documents/{id}/print`
- Enhanced report endpoint:
  - `GET /api/non-gst-goods/report`
- Report now returns:
  - purchase count / quantity / amount
  - sale count / quantity / amount
  - discount amount
  - sale cost amount
  - gross profit
  - current Non-GST stock quantity
  - current Non-GST stock value
  - document rows
  - current stock rows

### Purchase memo

- Supports multiple product lines in one memo.
- Each purchase line creates or updates a stock row with `IsOFB = true`.
- Captures item cost and MRP.
- Keeps GST fields at zero.

### Sale cash memo

- Supports multiple stock items in one cash memo.
- Sells only from `IsOFB = true` stock.
- Captures item cost snapshot for profit report.
- Prints cash memo with tax information showing GST 0%, tax amount 0.

### Frontend

Updated page:

- `frontend/garmetix-web/pages/non-gst-goods/index.vue`

Page now includes:

- Sale Cash Memo tab
- Purchase Memo tab
- Reports tab
- multi-line item entry
- post and print workflow
- printable memo modal
- sale / purchase / profit report
- current Non-GST stock report

## Compliance boundary

This module does not hide transactions. It records Non-GST / out-of-scope goods separately, posts visible accounting entries, excludes them from GST reports only because GST tax is zero/out-of-scope, and provides separate reports for stock, purchase, sale and profit.

# Stage 11D-88 - Vyapar Sale Import Preview

Version: **4.12.03**
Build code: **GARMETIX-11D88-20260628-4203**

## Purpose

Add a sales import workflow for Vyapar app Sale Report Excel files.

## Added

- Backend module: `backend/Garmetix.Api/SaleImport/`
- New API group: `/api/sale-import/vyapar`
- New frontend page: `/billing/vyapar-import`
- Sales menu link: `Sales -> Vyapar Sale Import`

## API

- `POST /api/sale-import/vyapar/preview`
  - accepts `.xlsx` or `.csv`
  - reads `Sale Report` and `Item Details`
  - matches Vyapar `Item Code` to Garmetix stock barcode
  - flags duplicate invoice numbers
  - flags missing product/stock and insufficient stock
  - returns grouped invoice preview and missing product list

- `POST /api/sale-import/vyapar/confirm`
  - posts reviewed invoices to normal `SalesInvoices`, `InvoiceItems`, `InvoicePayments`, `StockMovements` and accounting journals
  - skips duplicate invoices
  - supports preserving Vyapar invoice numbers
  - supports optional missing product/stock creation
  - supports optional stock bridge for historical imports where stock is insufficient
  - requires default bank account for non-cash receipts

## Important behavior

- Vyapar `Item Code` is treated as barcode candidate.
- If barcode exists in Garmetix stock, the line is ready.
- If barcode does not exist, user can fill override barcode or enable create product/stock.
- Product/stock creation uses imported sale line data and creates an opening bridge movement before sale-out, so imported historical sales can be posted without negative stock.
- Duplicate source invoice numbers are skipped during confirm.

## Runtime test checklist

1. Upload `SaleReport_01_04_26_to_30_06_26_ Apr_june_2026.xlsx`.
2. Verify preview counts and missing barcode rows.
3. Export missing barcode CSV.
4. Fill override barcode for at least one missing line or enable product/stock creation.
5. Select bank account for non-cash receipts.
6. Confirm small date range/test file first.
7. Verify invoice appears in Billing list.
8. Verify stock movement source type `VyaparSaleImport`.
9. Verify payment split and accounting journal.

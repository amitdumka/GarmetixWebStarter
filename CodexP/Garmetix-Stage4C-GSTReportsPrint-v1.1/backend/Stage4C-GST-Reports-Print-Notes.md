# Stage 4C — GST Reports, HSN Summary, and Print Finalization

Base package: `Garmetix-Stage4B-BillDiscountStockOps-v1.0.zip`

## Goal

Stage 4C focuses on GST reporting accuracy after the previous model alignment, item snapshot, GST split, bill-discount allocation, stock movement, and purchase return stages.

## Implemented

### 1. Dedicated GST report DTOs

Added `backend/Garmetix.Api/GstReturns/GstReportDtos.cs` with report contracts for:

- HSN summary report
- GST rate reconciliation report
- invoice register report

### 2. New GST report backend endpoints

Added the following endpoints under `/api/gst-returns/reports`:

- `GET /api/gst-returns/reports/hsn-summary`
- `GET /api/gst-returns/reports/hsn-summary/csv`
- `GET /api/gst-returns/reports/tax-summary`
- `GET /api/gst-returns/reports/tax-summary/csv`
- `GET /api/gst-returns/reports/invoice-register`
- `GET /api/gst-returns/reports/invoice-register/csv`

Common query parameters:

- `returnPeriod=MMYYYY`
- `direction=both|sales|purchase` for HSN and invoice register
- `companyId` optional; falls back to selected workspace company claim

### 3. Fixed book-based GSTR-1 HSN source

The existing book-based GSTR-1 builder was using product barcode as HSN fallback. Stage 4C now uses:

1. `InvoiceItem.HSNCode` snapshot
2. `Product.HSNCode` fallback
3. `0000` only when no HSN exists

It also maps unit snapshot to a basic GST UQC value such as `PCS`, `MTR`, `KGS`, `SET`, `PRS`, or `NOS`.

### 4. GST split preservation

Book-based GSTR-1 now uses stored line-level `IGSTAmount`, `CGSTAmount`, and `SGSTAmount` when available. It only falls back to split calculation when stored split fields are zero/missing.

### 5. New frontend GST Reports page

Added:

- `frontend/garmetix-web/pages/gst-reports/index.vue`

The page shows:

- HSN summary table
- GST rate reconciliation table
- invoice register table
- output tax metric
- input tax metric
- net payable metric
- CSV downloads for each report

Added sidebar entry:

- **GST Reports**

### 6. Receipt/PDF print finalization

Updated sale receipt item DTO and sale PDF rendering to carry and print:

- HSN code
- unit
- CGST
- SGST
- IGST

Updated purchase PDF rendering to print:

- HSN code
- unit
- GST rate in item lines
- CGST/SGST/IGST totals

## Important behavior notes

- Sales returns are treated as negative values in GST summary/report rows when `ReturnInvoice = true`.
- Purchase invoices are included as inward/input tax rows unless cancelled.
- Partial purchase return debit-note impact is not yet item-HSN level because the current partial return implementation does not persist a separate purchase-return item table. That should be addressed in a later stage if exact item-wise ITC reversal reporting is needed.

## Next recommended stage

Stage 4D — test coverage and data consistency verification:

- backend integration tests for sale/purchase/GST report math
- seed-data smoke test
- Docker build verification
- report reconciliation checks against invoice totals

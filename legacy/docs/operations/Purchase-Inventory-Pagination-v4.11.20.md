# Purchase and Inventory Pagination Performance Fix v4.11.20

## Scope

This release prevents large imported purchase and stock datasets from being loaded in one browser request.

## Backend

- Added `GET /api/purchase/invoices` with month/year, status, vendor, search, page and pageSize filters.
- Added `GET /api/inventory/product-master/paged` with search, stock mode, category, sub-category, brand, vendor, age bucket, page and pageSize filters.
- Kept legacy `purchase/invoices/recent` and `inventory/product-master` endpoints for backward compatibility.
- Added runtime performance indexes for purchase invoices, invoice items, stocks, product details and stock movements.

## Frontend

- `/purchase` defaults to current month and current year with server-side pagination.
- `/inventory` defaults to in-stock products only, with filters for out-of-stock/all, category, subcategory, age bucket and page size.
- Both pages support page sizes 25, 50, 100 and 200.

## Acceptance

- Purchase page does not load all purchase invoices after bulk import.
- Inventory page does not load all product/stock rows after bulk import.
- Current stock rows are shown by default; out-of-stock rows are available via filter.

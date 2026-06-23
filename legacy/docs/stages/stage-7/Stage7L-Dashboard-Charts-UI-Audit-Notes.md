# Stage 7L — Dashboard Charts and UI Layout Audit

Version: 3.11.0  
Stage: Stage 7L  
Build Code: GARMETIX-7L-20260610-3110

## Purpose

This stage closes the remaining Stage 7 dashboard polish work before moving into Stage 8 business expansion. It adds richer dashboard chart data, GST vs Non-GST visual split panels, stock/profit split panels, and a dedicated UI Layout Audit page to track full-page spacing and overlap checks.

## Backend changes

- Updated `backend/Garmetix.Api/Dashboard/DashboardDtos.cs`:
  - Added `DashboardBreakdownDto`.
  - Extended `DashboardTrendPointDto` with `Profit`, `NonGstSales`, and `NonGstPurchase`.
  - Added `RevenueBreakdown`, `StockBreakdown`, and `ProfitBreakdown` to both dashboard payloads.

- Updated `backend/Garmetix.Api/Dashboard/DashboardEndpoints.cs`:
  - Calculates regular/GST sales and Non-GST sales separately.
  - Calculates on-book stock valuation and Non-GST stock valuation separately.
  - Calculates GST margin, Non-GST margin, and total operational margin.
  - Trend chart data now includes profit and Non-GST values.

## Frontend dashboard changes

- Added `frontend/garmetix-web/components/dashboard/BreakdownGrid.vue`.
- Enhanced `frontend/garmetix-web/components/dashboard/TrendChart.vue`.
- Updated `/dashboard/store-manager` with:
  - Sales split panel.
  - Stock split panel.
  - Profit split panel.
- Updated `/dashboard/business` with:
  - Revenue split panel.
  - Stock valuation split panel.
  - Profit split panel.

## UI audit changes

- Added `/ui-audit` page.
- Added Admin sidebar/menu access for UI Layout Audit.
- Added access control rule for `/ui-audit`.
- Added checklist for:
  - margin and padding,
  - table containment,
  - card spacing,
  - responsive layout,
  - modal/slideover spacing,
  - sidebar/topbar overlap prevention,
  - industry-standard button and form alignment.

## CSS guardrails

Updated `frontend/garmetix-web/assets/css/main.css` with Stage 7L layout guardrails:

- dashboard breakdown bars,
- richer trend chart bars,
- UI audit cards,
- global min-width protection for grid/flex children,
- table and overflow containment,
- responsive stacking for breakdown panels.

## Version identity

Updated:

- `frontend/garmetix-web/utils/appVersion.ts`
- `frontend/garmetix-web/package.json`
- `frontend/garmetix-web/package-lock.json`
- `backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs`

## Revert safety

No page was removed. Legacy shell is still available:

```bash
NUXT_PUBLIC_DASHBOARD_SHELL=legacy
```

## Next recommended work

Stage 8 should begin only after your local Docker/runtime check. Recommended Stage 8A: module-by-module old-page UI standardization using the `/ui-audit` queue.

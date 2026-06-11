# Stage 7 Implementation Map

## Current Version

- Version: 3.11.0
- Stage: Stage 7L
- Build Code: GARMETIX-7L-20260610-3110

## Layout / Shell

- `frontend/garmetix-web/components/AppShell.vue` — active Nuxt UI dashboard-style shell.
- `frontend/garmetix-web/components/AppShellLegacy.vue` — rollback shell.
- `frontend/garmetix-web/composables/useAccessControl.ts` — central route/menu access policy.
- `frontend/garmetix-web/middleware/auth.global.ts` — session and page access guard.

## Dashboard Pages

- `/dashboard` — smart role-based dashboard redirect.
- `/dashboard/store-manager` — current-store dashboard.
- `/dashboard/business` — owner/admin/accountant dashboard.
- `/dashboard/map` — dashboard route and access map.
- `/system-info` — version, runtime and route audit.
- `/ui-audit` — page spacing, padding, responsive overlap and industry-standard layout audit queue.

## Reusable Dashboard Components

- `components/dashboard/PageHero.vue` — shared dashboard hero with badge, subtitle and refresh action.
- `components/dashboard/MetricGrid.vue` — shared KPI cards with loading skeletons.
- `components/dashboard/ActionGrid.vue` — shared quick action cards.
- `components/dashboard/HealthGrid.vue` — shared health/signal cards.
- `components/dashboard/TrendChart.vue` — shared sales/purchase/profit/Non-GST bar trend.
- `components/dashboard/BreakdownGrid.vue` — shared GST vs Non-GST, stock split and profit split breakdown panels.
- `components/dashboard/ItemList.vue` — shared recent activity/work queue/alert list.
- `components/dashboard/DataTable.vue` — shared dashboard table with money/number formatting.
- `components/dashboard/ExportActions.vue` — reusable dashboard snapshot card with JSON, CSV and print/PDF actions.
- `components/dashboard/FilterBar.vue` — reusable dashboard date range, auto-refresh and preference control card.

## Dashboard Preferences

- `frontend/garmetix-web/composables/useDashboardPreferences.ts`
  - Browser-local dashboard range preference.
  - Today / 7 days / 30 days / this month / custom dates.
  - Auto-refresh preference and interval.
  - Query parameter builder for `from` and `to`.

## Backend Dashboard API

- `backend/Garmetix.Api/Dashboard/DashboardDtos.cs`
- `backend/Garmetix.Api/Dashboard/DashboardEndpoints.cs`
- `GET /api/dashboard/home`
- `GET /api/dashboard/store-manager?from=YYYY-MM-DD&to=YYYY-MM-DD`
- `GET /api/dashboard/business?from=YYYY-MM-DD&to=YYYY-MM-DD`
- Dashboard payloads now include `revenueBreakdown`, `stockBreakdown` and `profitBreakdown`.

## Stage 7K Dashboard Filter Map

- `pages/dashboard/store-manager/index.vue`
  - Adds filter card, date range query, saved preferences, auto-refresh and last refreshed state.
- `pages/dashboard/business/index.vue`
  - Adds the same filter/preference behavior for owner/admin/accountant dashboard.
- `DashboardEndpoints.cs`
  - Accepts optional `from` and `to` filters.
  - Adds `DashboardPeriodDto` to dashboard payloads.
  - Uses selected period for sales, purchases, invoices, Non-GST metrics, trends and performance tables.

## Version Identity

- Backend: `backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs`
- Frontend: `frontend/garmetix-web/utils/appVersion.ts`
- Frontend package version: `frontend/garmetix-web/package.json`

## Rollback

Set this before build/restart to use the old shell:

```bash
NUXT_PUBLIC_DASHBOARD_SHELL=legacy
```

Stage 7K does not remove any page and keeps existing dashboard routes intact.


## Stage 7L Dashboard Charts and UI Audit Map

- `backend/Garmetix.Api/Dashboard/DashboardDtos.cs`
  - Adds `DashboardBreakdownDto`.
  - Extends trend points with profit and Non-GST sales/purchase values.
- `backend/Garmetix.Api/Dashboard/DashboardEndpoints.cs`
  - Calculates GST sales, Non-GST sales, on-book stock, Non-GST stock, GST margin, Non-GST margin and total margin.
- `frontend/garmetix-web/components/dashboard/BreakdownGrid.vue`
  - Reusable visual breakdown panel with proportional bars.
- `frontend/garmetix-web/pages/dashboard/store-manager/index.vue`
  - Adds sales, stock and profit split panels.
- `frontend/garmetix-web/pages/dashboard/business/index.vue`
  - Adds revenue, stock valuation and profit split panels.
- `frontend/garmetix-web/pages/ui-audit/index.vue`
  - Adds full page-by-page UI layout audit queue.
- `frontend/garmetix-web/assets/css/main.css`
  - Adds Stage 7L responsive spacing and overlap guardrails.

## Stage 7L v3.11.1 Docker Buildfix Map

Changed:
- `frontend/garmetix-web/Dockerfile`
  - Adds build-stage `NODE_OPTIONS=--max-old-space-size=4096`.
  - Adds `NUXT_TELEMETRY_DISABLED=1`.
  - Adds runtime `NODE_OPTIONS=--max-old-space-size=1024`.
- `frontend/garmetix-web/utils/appVersion.ts`
  - Version bumped to 3.11.1.
- `backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs`
  - Version bumped to 3.11.1.

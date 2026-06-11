# Stage 7 Implementation Map

## Current Version

- Version: 3.10.0
- Stage: Stage 7K
- Build Code: GARMETIX-7K-20260610-3100

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

## Reusable Dashboard Components

- `components/dashboard/PageHero.vue` — shared dashboard hero with badge, subtitle and refresh action.
- `components/dashboard/MetricGrid.vue` — shared KPI cards with loading skeletons.
- `components/dashboard/ActionGrid.vue` — shared quick action cards.
- `components/dashboard/HealthGrid.vue` — shared health/signal cards.
- `components/dashboard/TrendChart.vue` — shared sales/purchase bar trend.
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

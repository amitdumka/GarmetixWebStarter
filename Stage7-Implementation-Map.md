# Stage 7 Implementation Map

## Current Version

- Version: 3.8.0
- Stage: Stage 7I
- Build Code: GARMETIX-7I-20260610-380

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

## Stage 7I Reusable Dashboard Components

- `components/dashboard/PageHero.vue` — shared dashboard hero with badge, subtitle and refresh action.
- `components/dashboard/MetricGrid.vue` — shared KPI cards with loading skeletons.
- `components/dashboard/ActionGrid.vue` — shared quick action cards.
- `components/dashboard/HealthGrid.vue` — shared health/signal cards.
- `components/dashboard/TrendChart.vue` — shared 7-day sales/purchase bar trend.
- `components/dashboard/ItemList.vue` — shared recent activity/work queue/alert list.
- `components/dashboard/DataTable.vue` — shared dashboard table with money/number formatting.

## Backend Dashboard API

- `backend/Garmetix.Api/Dashboard/DashboardDtos.cs`
- `backend/Garmetix.Api/Dashboard/DashboardEndpoints.cs`
- `GET /api/dashboard/home`
- `GET /api/dashboard/store-manager`
- `GET /api/dashboard/business`

## Version Identity

- Backend: `backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs`
- Frontend: `frontend/garmetix-web/utils/appVersion.ts`
- Frontend package version: `frontend/garmetix-web/package.json`

## Rollback

Set this before build/restart to use the old shell:

```bash
NUXT_PUBLIC_DASHBOARD_SHELL=legacy
```

Stage 7I does not remove any page and does not change dashboard API contracts.

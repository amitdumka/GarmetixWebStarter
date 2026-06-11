# Stage 7K - Dashboard Filters, Preferences and Auto Refresh

Version: 3.10.0  
Stage: Stage 7K  
Build Code: GARMETIX-7K-20260610-3100

## Purpose

Stage 7K continues the Stage 7 Nuxt UI dashboard rollout by adding practical dashboard period controls and saved browser preferences. It also records the required full-page UI layout audit in the Stage 7 TODO list.

## Implemented

- Added reusable dashboard filter component:
  - `frontend/garmetix-web/components/dashboard/FilterBar.vue`
- Added dashboard preference composable:
  - `frontend/garmetix-web/composables/useDashboardPreferences.ts`
- Added dashboard presets:
  - Today
  - Last 7 days
  - Last 30 days
  - This month
  - Custom from/to dates
- Added auto-refresh controls:
  - Enable/disable auto refresh
  - 30 sec / 1 min / 2 min / 5 min intervals
  - Last refreshed timestamp
- Added saved browser-local dashboard preferences per dashboard.
- Updated `/dashboard/store-manager` with filter bar, date query and auto-refresh.
- Updated `/dashboard/business` with filter bar, date query and auto-refresh.
- Updated dashboard backend endpoints to accept optional `from` and `to` query parameters.
- Added dashboard period metadata to dashboard API payloads.
- Dashboard KPIs, trends, recent activity and performance tables now use the selected period where relevant.
- Added CSS for filter bar and spacing guardrails.
- Updated `Stage7-TODO.md` with a required UI layout audit:
  - margins
  - padding
  - gaps
  - overlap prevention
  - responsive spacing
  - table overflow safety
  - industry-standard layout consistency

## Files changed

- `backend/Garmetix.Api/Dashboard/DashboardDtos.cs`
- `backend/Garmetix.Api/Dashboard/DashboardEndpoints.cs`
- `backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs`
- `frontend/garmetix-web/components/dashboard/FilterBar.vue`
- `frontend/garmetix-web/composables/useDashboardPreferences.ts`
- `frontend/garmetix-web/pages/dashboard/store-manager/index.vue`
- `frontend/garmetix-web/pages/dashboard/business/index.vue`
- `frontend/garmetix-web/assets/css/main.css`
- `frontend/garmetix-web/utils/appVersion.ts`
- `frontend/garmetix-web/package.json`
- `frontend/garmetix-web/package-lock.json`
- `Stage7-TODO.md`
- `Stage7-Implementation-Map.md`

## API behavior

The dashboard endpoints now support:

```text
GET /api/dashboard/store-manager?from=2026-06-01&to=2026-06-10
GET /api/dashboard/business?from=2026-06-01&to=2026-06-10
```

Dates are inclusive at the UI level. The backend converts the end date to an exclusive boundary internally for safe querying.

## Revert safety

- No page was removed.
- Existing dashboard routes remain unchanged.
- `from` and `to` are optional, so old callers continue working.
- Legacy shell revert still works:

```bash
NUXT_PUBLIC_DASHBOARD_SHELL=legacy
```

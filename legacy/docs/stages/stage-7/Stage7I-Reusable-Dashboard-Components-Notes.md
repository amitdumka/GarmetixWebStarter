# Stage 7I — Reusable Dashboard Components and Widget Polish

## Version

- Version: 3.8.0
- Stage: Stage 7I
- Build Code: GARMETIX-7I-20260610-380
- Release name: Reusable Dashboard Components and Widget Polish

## Purpose

Stage 7I continues the Nuxt UI Dashboard conversion by reducing repeated dashboard markup and moving store/business dashboard widgets into reusable components. This keeps the current routes, menus and API contracts unchanged while making the dashboard easier to maintain and polish in future stages.

## Added Components

Created reusable Nuxt/Vue dashboard components under:

```text
frontend/garmetix-web/components/dashboard/
```

Files added:

- `PageHero.vue` — shared dashboard hero with badge, title, subtitle, loading/live state and refresh action.
- `MetricGrid.vue` — shared KPI metric cards with loading skeletons.
- `ActionGrid.vue` — shared quick-action card grid.
- `HealthGrid.vue` — shared operational/business health signals.
- `TrendChart.vue` — shared 7-day sales/purchase mini bar chart.
- `ItemList.vue` — shared recent activity, queue and alert list.
- `DataTable.vue` — shared dashboard table with money/number formatting.

## Updated Pages

Updated these pages to use the shared components:

- `frontend/garmetix-web/pages/dashboard/store-manager/index.vue`
- `frontend/garmetix-web/pages/dashboard/business/index.vue`

The dashboard API calls remain the same:

- `GET /api/dashboard/store-manager`
- `GET /api/dashboard/business`

## Behavior Preserved

- All current pages remain available.
- Sidebar/topbar layout is unchanged from Stage 7H.
- Permission-aware menu and route guards remain active.
- Legacy shell revert remains available using:

```bash
NUXT_PUBLIC_DASHBOARD_SHELL=legacy
```

## Improved UX

- Consistent empty states for dashboards.
- Consistent loading skeletons for metric cards.
- Consistent card headers and data-list display.
- Store manager and business dashboards now share the same visual system.
- Future dashboard widgets can be added by reusing the component set rather than copying markup.

## Validation

Static validation passed using:

```bash
python3 scripts/validation/stage7i-static-checks.py
```

Docker, `dotnet build`, and full Nuxt build were not run in this sandbox.

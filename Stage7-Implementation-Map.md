# Stage 7 Implementation Map

## Current package

- Version: 3.1.0
- Stage: Stage 7B
- Build code: GARMETIX-7B-20260610-310

## Frontend

- `components/AppShell.vue`: Nuxt UI dashboard shell, collapsible sidebar, topbar, smart dashboard shortcut.
- `components/AppShellLegacy.vue`: preserved revert shell.
- `pages/dashboard/index.vue`: role-aware smart landing page.
- `pages/dashboard/store-manager/index.vue`: current-store dashboard with KPIs, quick actions, health signals, trend, recent sales, low stock.
- `pages/dashboard/business/index.vue`: owner/admin/accountant dashboard with company/store-group KPIs, store table, store-group table and control queue.
- `utils/appVersion.ts`: frontend version identity.

## Backend

- `Dashboard/DashboardDtos.cs`: dashboard records for metrics, trend, actions, health signals, store and store-group performance.
- `Dashboard/DashboardEndpoints.cs`: `/api/dashboard/home`, `/api/dashboard/store-manager`, `/api/dashboard/business`.
- `AppInfo/AppInfoEndpoints.cs`: backend version identity.

## Revert

Set `NUXT_PUBLIC_DASHBOARD_SHELL=legacy` before frontend build/restart to use the pre-Stage-7 shell.

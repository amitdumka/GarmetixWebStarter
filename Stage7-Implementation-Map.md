# Stage 7 Implementation Map

## Current version

- Version: 3.5.0
- Stage: Stage 7F
- Build Code: GARMETIX-7F-20260610-350

## Main shell

- New shell: `frontend/garmetix-web/components/AppShell.vue`
- Legacy shell: `frontend/garmetix-web/components/AppShellLegacy.vue`
- Revert flag: `NUXT_PUBLIC_DASHBOARD_SHELL=legacy`

## Dashboard routes

- `/dashboard` — smart role-aware landing.
- `/dashboard/store-manager` — current-store dashboard.
- `/dashboard/business` — owner/admin/accountant dashboard.
- `/dashboard/map` — Stage 7 implementation map and revert notes.
- `/` — preserved legacy overview.

## Backend dashboard routes

- `GET /api/dashboard/home`
- `GET /api/dashboard/store-manager`
- `GET /api/dashboard/business`

## Stage 7 shell and menu changes

- Stage 7A: introduced Nuxt UI dashboard-style shell, sidebar, topbar and role dashboard routes.
- Stage 7B: added smart `/dashboard` and deeper role/workspace dashboard KPIs.
- Stage 7C: added breadcrumbs, favorites, recent pages, dashboard map and Ctrl/Cmd+K command search.
- Stage 7D: added controlled `UDashboardSidebar` collapse state, footer account dropdown and primary/utility navigation.
- Stage 7E: removed bulky sidebar status card and moved status to compact topbar/context/footer dropdowns.
- Stage 7F: removed duplicate Help and Account groups from the main sidebar; removed standalone Workspace sidebar footer item; workspace remains available through Status & Workspace, context store button and topbar mobile action.

## Footer/status destinations

- Account footer dropdown keeps: My Profile, Smart Dashboard, Dashboard Map, Message Logs, About, Contact, FAQ, Logout.
- Status & Workspace footer dropdown keeps: current store/company/time/API/version, Change workspace, System Health, Message Logs and About Version.
- Main sidebar utility navigation now contains only Admin.

## Version identity

- Frontend: `frontend/garmetix-web/utils/appVersion.ts`
- Backend: `backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs`
- Runtime check: `/api/app-info/version`

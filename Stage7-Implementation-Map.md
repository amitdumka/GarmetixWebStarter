# Stage 7 Implementation Map

## Current version

- Version: 3.3.0
- Stage: Stage 7D
- Build Code: GARMETIX-7D-20260610-330

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

## Stage 7C UX additions

- Breadcrumb/context bar inside `AppShell.vue`.
- Favorites stored in browser localStorage key `garmetix.favoritePaths`.
- Recent pages stored in browser localStorage key `garmetix.recentPaths`.
- Command search opens with Ctrl/Cmd + K.
- Dashboard Map page shows preserved menu groups, dashboard links and version identity.

## Version identity

- Frontend: `frontend/garmetix-web/utils/appVersion.ts`
- Backend: `backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs`
- Runtime check: `/api/app-info/version`


## Stage 7D / v3.3.0
- Controlled `UDashboardSidebar` collapse state with Ctrl+B and header/topbar collapse buttons.
- Refactored sidebar into primary navigation plus bottom utility navigation similar to Nuxt UI Dashboard template.
- Added footer account dropdown with profile, dashboard, help, logs and logout actions.
- Preserved `NUXT_PUBLIC_DASHBOARD_SHELL=legacy` revert option.

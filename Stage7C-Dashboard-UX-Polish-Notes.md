# Stage 7C — Dashboard UX Polish, Command Memory and Map

Version: 3.2.0  
Stage: Stage 7C  
Build Code: GARMETIX-7C-20260610-320

## Purpose

Continue Stage 7 after the Nuxt UI Dashboard shell and role dashboards. This stage keeps all existing pages and menus, improves the dashboard-template user experience, and documents the running dashboard layout from inside the app.

## Implemented

- Added Dashboard Map page: `/dashboard/map`.
- Added Dashboard Map menu link under **Dashboards**.
- Added breadcrumb/context bar above page content.
- Added role badge in dashboard context bar.
- Added one-click current page favorite action in topbar.
- Added local browser favorites for menu pages.
- Added local browser recent pages.
- Added Ctrl/Cmd + K keyboard shortcut for command search.
- Enhanced command/search modal with Favorites and Recent sections.
- Added Dashboard Map shortcut in context bar.
- Preserved all current menu links and pages.
- Preserved legacy shell revert option: `NUXT_PUBLIC_DASHBOARD_SHELL=legacy`.
- Updated visible version identity in frontend and backend.

## Files changed

- `frontend/garmetix-web/components/AppShell.vue`
- `frontend/garmetix-web/assets/css/main.css`
- `frontend/garmetix-web/pages/dashboard/map/index.vue`
- `frontend/garmetix-web/utils/appVersion.ts`
- `backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs`
- `Stage7-TODO.md`
- `Stage7-Implementation-Map.md`

## Revert / safety

No page was removed. The old shell is still available through:

```bash
NUXT_PUBLIC_DASHBOARD_SHELL=legacy
```

The old `/` legacy overview page is still linked as **Legacy Overview**.

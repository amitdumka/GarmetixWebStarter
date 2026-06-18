# Stage 7G — Permission-Aware Dashboard Access

Version: 3.6.0  
Build Code: GARMETIX-7G-20260610-360

## Goal

Stage 7G continues the Stage 7 dashboard migration by making the Nuxt UI dashboard shell role-aware and permission-aware at the frontend route/menu level, while preserving existing backend authorization policies.

## Implemented

- Added central frontend access policy map:
  - `frontend/garmetix-web/composables/useAccessControl.ts`
- Updated global auth middleware:
  - `frontend/garmetix-web/middleware/auth.global.ts`
- Added access denied page:
  - `frontend/garmetix-web/pages/access-denied.vue`
- Updated dashboard shell menu filtering:
  - `frontend/garmetix-web/components/AppShell.vue`
- Updated Dashboard Map to show the access matrix:
  - `frontend/garmetix-web/pages/dashboard/map/index.vue`
- Updated version identity:
  - frontend `utils/appVersion.ts`
  - backend `AppInfoEndpoints.cs`
  - frontend package version

## Access behavior

- Unauthenticated users are redirected to Login before protected pages render.
- Authenticated users without access are redirected to `/access-denied`.
- Menus hide pages that the current user cannot access.
- Command search, favorites, recent pages and sidebar menus share the same visibility filter.
- Footer/account dropdowns hide admin-only entries such as Message Logs for non-admin users.

## Role grouping

The central access map currently supports:

- `admin`
- `owner`
- `powerUser`
- `accountant`
- `remoteAccountant`
- `storeManager`
- `salesman`
- `member`
- `authenticated`

## Important note

This is not a replacement for backend security. Backend policies already protect API endpoints. Stage 7G adds matching frontend UX guards so users do not see or open pages they cannot use.

## Revert safety

The legacy shell revert remains available:

```bash
NUXT_PUBLIC_DASHBOARD_SHELL=legacy
```

No existing page was removed.

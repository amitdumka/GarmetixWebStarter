# Stage 7 Implementation Map

## Current Version

- Version: 3.6.0
- Stage: Stage 7G
- Build Code: GARMETIX-7G-20260610-360

## Shell files

- `frontend/garmetix-web/components/AppShell.vue`
- `frontend/garmetix-web/components/AppShellLegacy.vue`

## Stage 7G access files

- `frontend/garmetix-web/composables/useAccessControl.ts`
- `frontend/garmetix-web/middleware/auth.global.ts`
- `frontend/garmetix-web/pages/access-denied.vue`

## Dashboard pages

- `/dashboard`
- `/dashboard/store-manager`
- `/dashboard/business`
- `/dashboard/map`

## Backend dashboard endpoints

- `GET /api/dashboard/home`
- `GET /api/dashboard/store-manager`
- `GET /api/dashboard/business`

## Version identity

- Frontend: `frontend/garmetix-web/utils/appVersion.ts`
- Backend: `backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs`

## Revert option

Set this value before frontend build/restart:

```bash
NUXT_PUBLIC_DASHBOARD_SHELL=legacy
```

# Stage 7H - System Info and Dashboard Route Audit

Version: 3.7.0  
Build Code: GARMETIX-7H-20260610-370

## Purpose

Stage 7H completes the dashboard migration safety layer. It adds a central System Info page so the operator can confirm exactly which frontend and backend version is running, whether the API is healthy, which dashboard shell mode is active, and which routes are visible for the current logged-in user.

## Added

- Frontend page: `frontend/garmetix-web/pages/system-info/index.vue`
- Backend endpoint: `GET /api/app-info/system`
- App info DTO: `AppSystemInfoDto`
- Access rule: `/system-info` visible to Admin/Owner
- Status dropdown link: `System Info`
- Account/footer dropdown link: `System Info`
- Dashboard map entry for System Info
- Version bump to `3.7.0 / Stage 7H`
- API project assembly/package version updated to `3.7.0`

## System Info Page Shows

- frontend version/stage/build code
- backend version/stage/build code
- version match/mismatch badge
- API health status
- API environment
- server UTC time
- process start UTC time
- uptime
- assembly version
- current user role summary
- route audit by module
- visible/hidden route count for current user
- safe rollback reminder

## Revert Safety

No page or module was removed in Stage 7H. The legacy shell can still be enabled with:

```bash
NUXT_PUBLIC_DASHBOARD_SHELL=legacy
```

## Local Tests Recommended

```bash
cd backend
dotnet build

cd ../frontend/garmetix-web
npm ci
npm run build

cd ../..
docker compose up --build
```

Then open:

- `/system-info`
- `/dashboard/map`
- `/about-us`
- `/api/app-info/version`
- `/api/app-info/system`

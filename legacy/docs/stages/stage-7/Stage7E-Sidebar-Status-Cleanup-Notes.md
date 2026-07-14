# Stage 7E — Sidebar Status Cleanup

Version: 3.4.0  
Stage: Stage 7E  
Build Code: GARMETIX-7E-20260610-340

## Goal

Clean the Stage 7 dashboard sidebar so it follows the Nuxt UI dashboard style more closely. The sidebar should focus on navigation and account actions, not large status widgets.

## Changes

### Sidebar cleanup

Removed the bulky sidebar status card that showed:

- store name
- company name
- clock/date
- API live status
- version/revert hint

This was consuming too much sidebar height and did not match the dashboard-template style.

### New placement

The same information is now available in lighter, separate UI surfaces:

- Topbar status dropdown with API status, current store/company, date/time, and version.
- Context bar compact store button.
- Context bar API status badge.
- Sidebar footer compact buttons:
  - Workspace
  - Status
- Account dropdown remains in the sidebar footer.

### Sidebar footer

The footer now stays compact and template-like:

- Workspace quick button
- Status quick button
- User/account dropdown

### Version identity

Updated both frontend and backend version identity:

- `frontend/garmetix-web/utils/appVersion.ts`
- `backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs`
- `frontend/garmetix-web/package.json`

## Revert safety

The legacy shell revert option remains unchanged:

```bash
NUXT_PUBLIC_DASHBOARD_SHELL=legacy
```

## Files changed

- `frontend/garmetix-web/components/AppShell.vue`
- `frontend/garmetix-web/assets/css/main.css`
- `frontend/garmetix-web/utils/appVersion.ts`
- `frontend/garmetix-web/package.json`
- `frontend/garmetix-web/pages/dashboard/map/index.vue`
- `backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs`
- `scripts/validation/stage7e-static-checks.py`

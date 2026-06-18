# Stage 7F — Sidebar Menu Cleanup

Version: 3.5.0  
Stage: Stage 7F  
Build Code: GARMETIX-7F-20260610-350

## User request

Clean the dashboard sidebar further after Stage 7E:

- Remove the **Help** menu and its sub-menu from the main sidebar because those links already exist in the footer account menu.
- Remove the **Account** menu and its sub-menu from the main sidebar because those links already exist in the footer account menu.
- Remove the standalone **Workspace** menu/footer item because workspace is already available through the Status menu.

## Implemented

### Main sidebar cleanup

Updated:

- `frontend/garmetix-web/components/AppShell.vue`

Changes:

- Removed `Account` from `moduleGroups`.
- Removed `Help` from `moduleGroups`.
- Changed sidebar utility navigation from `['Account', 'Help', 'Admin']` to `['Admin']`.
- Removed Account/Help icons from `navigationGroupIcons` because those groups are no longer rendered in main sidebar navigation.

### Footer retained as primary place for account/help

Kept footer account dropdown intact:

- My Profile
- Smart Dashboard
- Dashboard Map
- Message Logs
- About Garmetix
- Contact Us
- FAQ
- Logout

This keeps the links available without taking extra main sidebar space.

### Workspace moved fully into status flow

Removed the standalone sidebar footer **Workspace** button.

Replaced it with a single footer status dropdown button:

- `Status & Workspace`

That dropdown still exposes:

- current store
- current company
- current date/time
- API status
- current version/stage
- Change workspace
- System Health
- Message Logs
- About Version

Workspace is also still available from:

- context bar store button
- mobile topbar workspace button

### Version identity

Updated:

- `frontend/garmetix-web/utils/appVersion.ts`
- `backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs`
- `frontend/garmetix-web/package.json`
- `frontend/garmetix-web/package-lock.json`

New identity:

- Version: `3.5.0`
- Stage: `Stage 7F`
- Build Code: `GARMETIX-7F-20260610-350`

## Revert safety

Legacy shell revert remains available:

```bash
NUXT_PUBLIC_DASHBOARD_SHELL=legacy
```

No existing page was removed. Only duplicate menu placement was cleaned.

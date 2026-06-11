# Stage 7D — Collapsible Sidebar and Dashboard Footer Menu

Version: 3.3.0  
Stage: Stage 7D  
Build Code: GARMETIX-7D-20260610-330

## Goal

Improve the Stage 7 dashboard shell so it behaves closer to the Nuxt UI Dashboard template:

- Sidebar must truly collapse into icon-only mode.
- Sidebar should use template-style primary navigation plus a lower/bottom utility navigation section.
- Footer should use a compact account/menu button that opens a small menu.
- Current pages and menus must remain available.
- Legacy shell revert option must remain available.

## Implemented

### AppShell changes

Updated:

```text
frontend/garmetix-web/components/AppShell.vue
```

Implemented:

- Controlled sidebar collapse state with `v-model:collapsed`.
- Controlled mobile/open state with `v-model:open`.
- Added explicit header collapse/expand button.
- Added keyboard shortcut:
  - `Ctrl+B` / `Cmd+B` to collapse or expand sidebar.
- Set dashboard group unit to `rem` and sidebar sizes:
  - default width: `20rem`
  - min width: `16rem`
  - max width: `26rem`
  - collapsed width: `4rem`
- Replaced flat navigation with dashboard-template style grouped navigation:
  - Primary section: Dashboards, Operations, People, Off Book.
  - Bottom utility section: Account, Help, Admin.
- Added bottom account dropdown menu with:
  - My Profile
  - Smart Dashboard
  - Dashboard Map
  - Message Logs
  - About Garmetix
  - Contact Us
  - FAQ
  - Logout
- Added topbar account dropdown as well.
- Preserved all current routes and pages.

### CSS changes

Updated:

```text
frontend/garmetix-web/assets/css/main.css
```

Added Stage 7D styling for:

- sidebar header alignment
- collapsed logo mode
- primary navigation scroll area
- bottom utility navigation
- account dropdown trigger
- command-search keyboard hint

### Version identity

Updated:

```text
frontend/garmetix-web/utils/appVersion.ts
backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs
frontend/garmetix-web/package.json
```

New identity:

```text
Version: 3.3.0
Stage: Stage 7D
Build Code: GARMETIX-7D-20260610-330
```

### Revert safety

Legacy shell remains available:

```bash
NUXT_PUBLIC_DASHBOARD_SHELL=legacy
```

No major page was removed.

## Test checklist

After build, test:

```text
/dashboard
/dashboard/map
/dashboard/store-manager
/dashboard/business
/profile
/message-logs
/about-us
```

UI checks:

- Click the sidebar collapse icon in the sidebar header.
- Click the topbar sidebar-collapse control.
- Press `Ctrl+B` or `Cmd+B`.
- Confirm collapsed mode shows icon-only menu.
- Confirm bottom Account/Help/Admin menu section remains usable.
- Confirm footer account button opens a dropdown.
- Confirm legacy shell still works with `NUXT_PUBLIC_DASHBOARD_SHELL=legacy`.

# Stage 7B — Dashboard Analytics, Role Routing and Template Alignment

Version: 3.1.0
Build code: GARMETIX-7B-20260610-310

## Re-checked Stage 7 instruction

- Use Nuxt UI Dashboard template style for the main web app shell.
- Keep current dark theme, sidebar, topbar, menus and pages.
- Keep sidebar collapsible and keep a safe revert option.
- Add a Store Manager dashboard based on the logged-in user current store.
- Add an Owner/Admin/Accountant dashboard with company-wise and store-group-wise visibility.
- Use role and permission/workspace restrictions.
- Ask before removing major pages. No major page was removed in this stage.
- Log TODO and implementation map.

## Implemented

- Added `/dashboard` smart landing route. It calls `/api/dashboard/home` and redirects to the correct dashboard.
- Added `/api/dashboard/home`.
- Added dashboard quick actions and health signals.
- Added store-group performance table to the business dashboard.
- Added smart dashboard menu item and topbar shortcut.
- Preserved all current pages and menus.
- Kept legacy shell revert option: `NUXT_PUBLIC_DASHBOARD_SHELL=legacy`.

## Nuxt UI template alignment

The dashboard template uses multi-page dashboard layout, collapsible sidebar, keyboard/search style command flow, light/dark mode and admin-dashboard structure. Stage 7B continues aligning Garmetix to those patterns without deleting existing screens.

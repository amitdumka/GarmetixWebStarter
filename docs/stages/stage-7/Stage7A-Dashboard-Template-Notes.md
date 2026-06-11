# Stage 7A — Nuxt UI Dashboard Shell and Role Dashboards

Version: 3.0.0  
Build Code: GARMETIX-7A-20260610-300

## Source analysis

The Nuxt UI dashboard template was used as the design reference. The public template highlights a multi-page dashboard, collapsible sidebar, keyboard-friendly search, light/dark mode, command style navigation and topbar-based admin shell. The project already uses Nuxt UI v4, so this stage adapts the existing Garmetix app rather than replacing the current pages.

## Implemented

- Added `components/AppShellLegacy.vue` as an exact copy of the previous shell.
- Replaced `components/AppShell.vue` with a v3 dashboard shell inspired by the Nuxt UI dashboard template.
- Preserved all existing current menu links and pages.
- Added dashboard routes:
  - `/dashboard/store-manager`
  - `/dashboard/business`
- Added backend dashboard endpoints:
  - `GET /api/dashboard/store-manager`
  - `GET /api/dashboard/business`
- Added `backend/Garmetix.Api/Dashboard/DashboardDtos.cs`.
- Added `backend/Garmetix.Api/Dashboard/DashboardEndpoints.cs`.
- Added runtime revert switch:
  - `NUXT_PUBLIC_DASHBOARD_SHELL=legacy`
- Added dark-mode polished shell CSS.
- Added command/search menu modal.
- Updated app version to Stage 7A / v3.0.0.

## Store Manager dashboard

Scope: current logged-in user's permitted store and selected workspace.

Shows:

- today sales
- month sales
- month purchase
- stock value
- invoices today
- low stock count
- seven-day sales/purchase trend
- recent sales
- low stock alerts
- store-manager work queue

## Owner/Admin/Accountant dashboard

Scope: permitted company/store group/store based on claims and workspace selection.

Shows:

- month sales
- month purchase
- gross margin
- stock value
- invoice count
- customer/vendor count
- seven-day trend
- store-wise performance table
- recent sales
- recent purchases
- admin/accounting queue

## Pages not removed

No major page was removed. The previous shell is preserved as `AppShellLegacy.vue`, and the old `/` overview page remains available as `Legacy Overview` in the Dashboards menu.

## Revert option

Set this in frontend runtime environment and rebuild/restart:

```bash
NUXT_PUBLIC_DASHBOARD_SHELL=legacy
```

This keeps the new Stage 7A backend endpoints and dashboard pages, but renders the previous app shell.

## Stage 7B planned

Stage 7B should add deeper analytics widgets, shortcuts, dashboard permission policies, chart components, saved dashboard filters, and optional command palette keyboard shortcuts.

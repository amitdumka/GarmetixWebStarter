# Stage 8B Package 3 - Permission-Aware Shell Notifications

Version: 4.1.2
Build: `GARMETIX-8B-20260614-4102`
Date: 2026-06-14

## Completed

- Replaced the unconditional Message Logs topbar button with a notification and quick-action menu available to every authenticated role.
- Added an authenticated notification API that scopes events by user, company, and store for non-administrators.
- Excluded frontend diagnostics, runtime startup messages, stack traces, request details, and payload data from the business notification response.
- Mapped failed business operations to friendly titles, short guidance, and a permitted module destination.
- Filtered notification destinations and quick actions through the same role route matrix used by menus and middleware.
- Added the notification menu to the expanded and collapsed sidebar footer and the dashboard topbar.
- Corrected Nuxt UI dashboard-panel slot usage so the topbar and mobile sidebar trigger actually render in both current and legacy shells.
- Persisted collapsed-sidebar preference and automatically closed the mobile drawer after navigation.
- Preserved active navigation state, favorites, recent pages, command search, workspace selection, status, theme, and account actions.

## Security and Privacy

- Full technical diagnostics remain available only on the administrator-only Message Logs page.
- Security notifications are returned only to Owner and Admin users.
- A role cannot receive a quick action or notification destination for a route outside its permission profile.

## Next

Begin Stage 8C with formal Purchase Return records, item snapshots, debit-note generation, stock reversal, and vendor settlement.

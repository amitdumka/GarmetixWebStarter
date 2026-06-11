# Stage 7 TODO

Stage 7 is complete through Stage 7M. Current implementation work is tracked in `../../planning/CURRENT-ROADMAP.md`.

## Completed

- Stage 7A: Nuxt UI Dashboard shell and role dashboards.
- Stage 7B: Smart dashboard route and deeper dashboard analytics.
- Stage 7C: Dashboard UX polish, favorites, recent pages, breadcrumbs and command search.
- Stage 7D: True collapsible sidebar and footer account menu.
- Stage 7E: Sidebar status cleanup and compact topbar/footer status surfaces.
- Stage 7F: Removed duplicate Help, Account and Workspace entries from main sidebar.
- Stage 7G: Permission-aware menus, command search and protected frontend routes.
- Stage 7H: System Info page, version match check and route audit.
- Stage 7I: Reusable dashboard widget components, shared loading skeletons, empty states, metric cards, trend chart, item lists and performance tables.
- Stage 7J: Dashboard JSON/CSV export and browser print/PDF snapshot tools.
- Stage 7K: Dashboard date-range filters, saved browser preferences, auto-refresh controls and layout audit TODO.
- Stage 7L: Richer dashboard charts, GST vs Non-GST breakdowns, stock/profit split panels and UI Layout Audit page.
- Stage 7M: Pre-v4.0 dashboard naming, heading-size, sidebar menu, login copy and visible release-label cleanup.

## Required UI Layout Audit Before Stage 8 / v4.0

- Check every page for proper outer margin and padding.
- Check every card, form, table, modal and toolbar for consistent spacing and alignment.
- Ensure no page content overlaps the sidebar, topbar, footer dropdowns, modals or floating controls.
- Ensure all pages have responsive mobile/tablet/desktop spacing.
- Ensure tables scroll horizontally inside cards instead of breaking layout.
- Ensure action buttons wrap cleanly and do not collide with filters or headings.
- Ensure all legacy pages follow the same industry-standard layout rhythm as the dashboard shell.
- Review naming, capitalization and sentence case on every page header, helper text, form label, empty state, alert and button.
- Remove internal stage numbers from visible business pages unless the page is specifically a technical release/system page.
- Add one reusable page/layout spacing utility if repeated fixes are needed.

## Completed before Stage 8 / v4.0

- Reduced large dashboard hero headings to normal page-title size.
- Renamed Store Manager dashboard menu/page context to Store dashboard.
- Renamed Owner/Admin dashboard menu/page context to Company dashboard.
- Moved Reports out of Dashboards.
- Moved GST Returns and GST Reports into GST.
- Moved Non-GST Goods into Off Book.
- Split Operations into Sales, Purchase, Inventory, Accounting, CRM, GST, Reports, Off Book and People.
- Split Admin tools into Admin, Data, Maintenance and System.
- Kept footer Account and Help links instead of duplicating them in the main sidebar.
- Renamed Status & Workspace footer control to Status.
- Simplified Login page copy and removed technical JWT helper badges.
- Changed sidebar subtitle to version number only.
- Set sidebar menu groups to open only for the selected/current route.

## Next recommended work

### Stage 8A — UI Audit Fix Pass / v4.0

- Use `/ui-audit` to complete a page-by-page visual pass.
- Standardize page headers, filter bars, empty states and table actions across older pages.
- Replace repeated custom snippets with reusable Nuxt UI components.
- Fix margin, padding, responsive spacing and overlap issues across the whole app.

### Stage 8B - User, Role and Permission Hardening / v4.1

- Audit and refine the existing user list/create/edit screens.
- Validate role, permission, company, store-group, and store assignment behavior.
- Validate password reset and active/inactive controls.
- Add deeper audit logging and automated permission-matrix coverage.

## Revert Safety

- Legacy shell remains available using `NUXT_PUBLIC_DASHBOARD_SHELL=legacy`.
- Existing pages and routes remain preserved.
- Stage 7M keeps dashboard shell and API contracts backward-compatible while cleaning visible UI names and menu structure.

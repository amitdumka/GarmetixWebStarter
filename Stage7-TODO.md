# Stage 7 TODO

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

## Required UI Layout Audit Before Stage 8

- Check every page for proper outer margin and padding.
- Check every card, form, table, modal and toolbar for consistent spacing and alignment.
- Ensure no page content overlaps the sidebar, topbar, footer dropdowns, modals or floating controls.
- Ensure all pages have responsive mobile/tablet/desktop spacing.
- Ensure tables scroll horizontally inside cards instead of breaking layout.
- Ensure action buttons wrap cleanly and do not collide with filters or headings.
- Ensure all legacy pages follow the same industry-standard layout rhythm as the Stage 7 dashboard shell.
- Add one reusable page/layout spacing utility if repeated fixes are needed.

## Next recommended stages

### Stage 7L — Dashboard Charts and Drilldowns

- Add richer sales/purchase/profit charts.
- Add GST vs Non-GST split charts.
- Add stock ageing and low-stock risk dashboard.
- Add store-group and company comparison drilldowns.
- Add widget-level CSV export where deeper drilldown data is needed.

### Stage 7M — Page Layout Standardization

- Complete the required UI layout audit page by page.
- Standardize page headers, filter bars, empty states and table actions across older pages.
- Replace repeated custom snippets with reusable Nuxt UI components.
- Fix margin, padding, responsive spacing and overlap issues across the whole app.

### Stage 7N — User Preferences

- Add default landing dashboard preference.
- Add favorite KPI/widget preferences.
- Add collapsed sidebar preference persisted per user.
- Add theme/accent preference persistence.

## Revert Safety

- Legacy shell remains available using `NUXT_PUBLIC_DASHBOARD_SHELL=legacy`.
- Existing pages and routes remain preserved.
- Stage 7K keeps dashboard shell and API contracts backward-compatible while adding optional `from` and `to` filters.

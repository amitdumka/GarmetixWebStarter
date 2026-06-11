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

## Next recommended stages

### Stage 7J — Runtime Build Fix Pass + Page Standardization

- Run Docker build locally and fix any Nuxt/.NET compile/runtime errors.
- Apply the reusable dashboard components to more pages where it improves consistency.
- Standardize page headers, filter bars, empty states and table actions across older pages.
- Replace repeated custom dashboard-style snippets with componentized UI blocks.

### Stage 7K — Dashboard Charts and Drilldowns

- Add sales/purchase/profit charts.
- Add GST vs Non-GST split.
- Add stock ageing and low-stock risk dashboard.
- Add store-group and company comparison drilldowns.
- Add CSV/PDF export for dashboard widgets.

### Stage 7L — User Preferences

- Add default landing dashboard preference.
- Add favorite KPI/widget preferences.
- Add collapsed sidebar preference persisted per user.
- Add theme/accent preference persistence.

## Revert Safety

- Legacy shell remains available using `NUXT_PUBLIC_DASHBOARD_SHELL=legacy`.
- Existing pages and routes remain preserved.
- Stage 7I only refactors dashboard page presentation into reusable components; dashboard API contracts are unchanged.

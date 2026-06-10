## Stage 7E completed

- Removed bulky clock/store/API/version card from sidebar.
- Moved status to compact topbar/context/footer controls.
- Preserved collapsible sidebar and legacy shell revert option.

# Stage 7 TODO

## Completed

- Stage 7A: Nuxt UI Dashboard-style shell with collapsible sidebar/topbar and role dashboard routes.
- Stage 7B: Smart `/dashboard` landing, store-manager dashboard, business dashboard, role/workspace analytics.
- Stage 7C: Dashboard UX polish, breadcrumbs, favorites, recent pages, keyboard command search and Dashboard Map page.

## Remaining candidates

- Stage 7D: deeper widgets/charts for sales, purchase, profit, stock and GST health.
- Stage 7E: dashboard personalization per role/user, saved filters and configurable cards.
- Stage 7F: command palette actions beyond navigation, such as create sale, create purchase, open customer, open stock.
- Stage 7G: notification center and dashboard alerts from message logs/system health.

## Safety policy

Do not remove old pages without explicit confirmation. Keep `NUXT_PUBLIC_DASHBOARD_SHELL=legacy` until the new dashboard shell is accepted in production.


## Stage 7D / v3.3.0
- Controlled `UDashboardSidebar` collapse state with Ctrl+B and header/topbar collapse buttons.
- Refactored sidebar into primary navigation plus bottom utility navigation similar to Nuxt UI Dashboard template.
- Added footer account dropdown with profile, dashboard, help, logs and logout actions.
- Preserved `NUXT_PUBLIC_DASHBOARD_SHELL=legacy` revert option.

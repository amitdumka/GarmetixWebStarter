# Stage 7 TODO

## Completed

- Stage 7A: Nuxt UI Dashboard-style shell with collapsible sidebar/topbar and role dashboard routes.
- Stage 7B: Smart `/dashboard` landing, store-manager dashboard, business dashboard, role/workspace analytics.
- Stage 7C: Dashboard UX polish, breadcrumbs, favorites, recent pages, keyboard command search and Dashboard Map page.
- Stage 7D: controlled collapsible sidebar, icon-only mode, footer account dropdown and Nuxt UI Dashboard-style primary/utility navigation.
- Stage 7E: removed bulky sidebar status card; moved clock/store/API/version status into compact topbar/context/footer surfaces.
- Stage 7F: removed duplicate Help and Account groups from main sidebar; removed standalone Workspace footer item and kept workspace inside Status & Workspace dropdown.

## Remaining candidates

- Stage 7G: deeper widgets/charts for sales, purchase, profit, stock and GST health.
- Stage 7H: dashboard personalization per role/user, saved filters and configurable cards.
- Stage 7I: command palette actions beyond navigation, such as create sale, create purchase, open customer, open stock.
- Stage 7J: notification center and dashboard alerts from message logs/system health.

## Safety policy

Do not remove old pages without explicit confirmation. Keep `NUXT_PUBLIC_DASHBOARD_SHELL=legacy` until the new dashboard shell is accepted in production.

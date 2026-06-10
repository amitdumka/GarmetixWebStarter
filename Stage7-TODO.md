# Stage 7 TODO

## Completed

- Stage 7A: Nuxt UI dashboard-style shell, collapsible sidebar, topbar, current menus preserved, role dashboard starter pages.
- Stage 7B: Smart dashboard landing, role-aware dashboard routing, quick actions, health signals, store-group performance.

## Next candidates

- Stage 7C: Replace simple CSS bar charts with Nuxt UI/Chart dashboard widgets once final analytics fields are stable.
- Stage 7D: Add dashboard personalization per user: pinned modules, preferred dashboard, compact/sidebar default.
- Stage 7E: Add notification center using message logs and stock/accounting alerts.
- Stage 7F: Add exportable dashboard PDF/print summary.

## Safe migration rule

Do not remove major existing pages without explicit approval. Keep legacy layout available through `NUXT_PUBLIC_DASHBOARD_SHELL=legacy`.

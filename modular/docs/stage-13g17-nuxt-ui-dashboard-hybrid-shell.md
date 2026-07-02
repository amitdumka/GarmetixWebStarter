# Stage 13G.17 - Nuxt UI Dashboard Hybrid Shell

Version: 5.13.57

## Decision

Use the Option A hybrid selected by Amit:

- Base shell: official Nuxt UI dashboard template structure.
- Dashboard page feel: NuxtCharts dashboard density and metric layout style.
- Garmetix adaptation: store/accounting operations remain dense, practical and role-oriented.

## Implemented

- Replaced the hand-built modular sidebar/topbar shell with Nuxt UI dashboard primitives:
  - `UDashboardGroup`
  - `UDashboardSidebar`
  - `UDashboardNavbar`
  - `UDashboardToolbar`
  - `UDashboardSearch`
  - `UNavigationMenu`
- Kept Garmetix branding, icons, dark theme, API status, clock, module app switcher and notifications.
- Preserved all existing modular routes and app wrappers.
- Kept the runtime app-id guard from Stage 13G.16.

## Next UI Work

- Convert major list pages to `UDashboardPanel` page-level layouts where needed.
- Use split-panel layouts for message logs, audit, inbox-style workflows and attendance review.
- Use NuxtCharts-style metric panels for dashboards and AI Sense pages.
- Review mobile behavior in the browser after deploy.

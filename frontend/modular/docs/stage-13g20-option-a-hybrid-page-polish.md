# Stage 13G.20 Option A Hybrid Page Polish

Version: 5.13.60

## Purpose

Start applying the approved Option A hybrid direction to actual module pages, not only the shared shell.

## Scope

- Back Office dashboards and reports through `MainDashboardReadModel`.
- AI Sense command center, business dashboard and connected analysis pages.
- Admin System Health and Message Logs pages.

## Design Direction

- Option A base: Nuxt UI dashboard shell, calm operational layout, compact actions and predictable navigation.
- Option B influence: metric cards, signal panels and report-style dashboard sections.
- Option C influence: denser log/status panels for diagnostics and audit-like pages.

## Implementation Notes

- Added shared surface classes for page stack, hero header, metric cards, panels, row cards and trend grids.
- Kept data contracts, endpoints and route ownership unchanged.
- Kept the current modular app split unchanged.

## Next Stage

- Extend the same surface system into Books logs/audit/GST reports and HR attendance/calendar pages.
- Then tighten page-specific responsive spacing for 14 inch laptop screens.

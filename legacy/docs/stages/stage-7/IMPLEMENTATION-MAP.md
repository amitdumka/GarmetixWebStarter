# Stage 7 Implementation Map

## Current Version

- Version: 3.12.0
- Stage: Stage 7M
- Build Code: GARMETIX-7M-20260611-3120
- Release: Pre-v4.0 UI Naming and Menu Cleanup

## Layout / Shell

- `frontend/garmetix-web/components/AppShell.vue` — active Nuxt UI dashboard-style shell.
- `frontend/garmetix-web/components/AppShellLegacy.vue` — rollback shell.
- `frontend/garmetix-web/composables/useAccessControl.ts` — central route/menu access policy.
- `frontend/garmetix-web/middleware/auth.global.ts` — session and page access guard.

## Dashboard Pages

- `/dashboard` — smart role-based dashboard redirect.
- `/dashboard/store-manager` — Store dashboard, titled by the current store name when available.
- `/dashboard/business` — Company dashboard, titled by the current company name when available.
- `/dashboard/map` — dashboard route and access map.
- `/system-info` — version, runtime and route audit.
- `/ui-audit` — page spacing, padding, responsive overlap and industry-standard layout audit queue.

## Sidebar Menu Structure

- Dashboards: Dashboard, Store, Company, Dashboard Map, Legacy Overview.
- Sales: Billing, Sales Return.
- Purchase: Purchase, Purchase Return.
- Inventory: Product Master, Stock Operations.
- Accounting: Accounting, Petty Cash, Vouchers, Debit Notes, Credit Notes, Commercial Summary.
- CRM: Customers, Parties & Vendors, Loyalty.
- GST: GST Returns, GST Reports.
- Reports: Reports Center.
- Off Book: Non-GST Goods, Cash Vouchers.
- People: HR, Payroll.
- Admin: Company Setup, Onboarding, AF/SS Seeder, Roles & Users.
- Data: Import / Export, Data Consistency, Message Logs, Audit Trail, UI Layout Audit.
- Maintenance: System Health, Production Readiness, Release Stabilization, Oracle Sync.
- System: System Info.

## Visible UI Cleanup

- Dashboard hero size reduced from large marketing-style heading to normal app page heading.
- Sidebar brand subtitle changed from dashboard-shell text to version number only.
- Footer status control renamed from Status & Workspace to Status.
- Login page simplified to Garmetix branding without JWT/session helper badges.
- Visible page headers no longer show internal stage numbers; module context names are used instead.
- Sidebar groups default-open only when their route is active/current.

## Reusable Dashboard Components

- `components/dashboard/PageHero.vue` — shared dashboard hero with compact heading, badge, subtitle and refresh action.
- `components/dashboard/MetricGrid.vue` — shared KPI cards with loading skeletons.
- `components/dashboard/ActionGrid.vue` — shared quick action cards.
- `components/dashboard/HealthGrid.vue` — shared health/signal cards.
- `components/dashboard/TrendChart.vue` — shared sales/purchase/profit/Non-GST bar trend.
- `components/dashboard/BreakdownGrid.vue` — shared GST vs Non-GST, stock split and profit split breakdown panels.
- `components/dashboard/ItemList.vue` — shared recent activity/work queue/alert list.
- `components/dashboard/DataTable.vue` — shared dashboard table with money/number formatting.
- `components/dashboard/ExportActions.vue` — reusable dashboard snapshot card with JSON, CSV and print/PDF actions.
- `components/dashboard/FilterBar.vue` — reusable dashboard date range, auto-refresh and preference control card.

## Backend Dashboard API

- `backend/Garmetix.Api/Dashboard/DashboardDtos.cs`
- `backend/Garmetix.Api/Dashboard/DashboardEndpoints.cs`
- `GET /api/dashboard/home`
- `GET /api/dashboard/store-manager?from=YYYY-MM-DD&to=YYYY-MM-DD`
- `GET /api/dashboard/business?from=YYYY-MM-DD&to=YYYY-MM-DD`

## Version Identity

- Backend: `backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs`
- Frontend: `frontend/garmetix-web/utils/appVersion.ts`
- Frontend package version: `frontend/garmetix-web/package.json`

## Rollback

Set this before build/restart to use the old shell:

```bash
NUXT_PUBLIC_DASHBOARD_SHELL=legacy
```

Stage 7M does not remove any page and keeps existing dashboard routes intact.

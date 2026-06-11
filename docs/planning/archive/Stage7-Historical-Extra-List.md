# Archived Stage 7 Extra List

This is a historical request/transcript and is not the current roadmap. Its outstanding items were reconciled into `../CURRENT-ROADMAP.md` and documented in `../TODO-RECONCILIATION.md`.

Reuest to Do: Now Start Stage 7 and Version 3.0 onwards, https://dashboard-template.nuxt.dev/ github https://github.com/nuxt-ui-templates/dashboard, documentation https://ui.nuxt.com/docs/getting-started/installation/nuxt , mcp server details MCP server URL: https://ui.nuxt.com/mcp Authentication: None, Analysis the this web site for Nuxt ui4 theme template of "Dashboard" implement the "Dashboard" for main layout of the our web app/site . use dark mode color theme which is already there in current, Side bar, Top bar, menus and Side bar parts , collapsible sidebar, Keep all current menu and pages and Dashboard Page For " Store manger " which will data will be based on for his current store. and Second Dashboard page for Owner/Admin/Accountant which has Store Group wise and Company wise. based their role and premission . Ask before removing this major page. keep option to revert the page. Do in two stage/part. and Log in todo and map

Thouaght to do : What I used from Nuxt UI Dashboard

I reviewed the Nuxt UI dashboard template and Nuxt UI installation docs. The dashboard template describes multiple pages, collapsible sidebar, keyboard shortcuts, light/dark mode, command palette, and Nuxt UI-powered admin UI patterns. UI docs confirm the current Nuxt UI setup style, @nuxt/ui module registration, dark mode support through Nuxt UI integrations, and the official ui/dashboard template path. mented in Stage 7A

Added new dashboard shell:

frontend/garmetix-web/components/AppShell.vue

Preserved old shell for revert:

frontend/garmetix-web/components/AppShellLegacy.vue

Added new dashboard pages:

frontend/garmetix-web/pages/dashboard/store-manager/index.vue
frontend/garmetix-web/pages/dashboard/business/index.vue

Added backend dashboard module:

backend/Garmetix.Api/Dashboard/DashboardDtos.cs
backend/Garmetix.Api/Dashboard/DashboardEndpoints.cs

Added endpoints:

GET /api/dashboard/store-manager
GET /api/dashboard/business

Mapped endpoint in:

backend/Garmetix.Api/Program.cs
Dashboard behavior

Store Manager Dashboard

Route:

/dashboard/store-manager

Shows current-store scoped data:

today sales
month sales
month purchase
stock value
invoices today
low-stock count
7-day sales/purchase trend
recent sales
low-stock alerts
daily work queue

Owner/Admin/Accountant Dashboard

Route:

/dashboard/business

Shows company/store-group/store scoped data based on role and workspace:

month sales
month purchase
gross margin
stock value
invoice count
customer/vendor count
7-day trend
store-wise performance table
recent sales
recent purchases
admin/accounting queue
Main layout changes
Nuxt UI dashboard-style shell
collapsible sidebar
topbar
dark-mode polished layout
current company/store-group/store selectors
command/search menu
all existing menu links preserved
old / overview page kept as Legacy Overview
no major page removed
Revert option

You can revert to the old shell without deleting the new code.

Set this environment value before build/restart:

what is left now
Current completed stage is Stage 7C / v3.2.0.


2. Stage 7D — Dashboard template final polish

Recommended next part:

make sidebar/topbar closer to Nuxt UI dashboard template
improve mobile sidebar drawer
add user dropdown in topbar
add notification/action dropdown
improve command palette styling
polish active menu state
improve collapsed sidebar icon-only mode
add loading skeletons for dashboard cards
improve empty/error states
3. Stage 7E — Permission-aware dashboard/menu

Currently the dashboards are role-aware at a basic level. Still left:

hide menu items based on permissions
protect pages based on permission
redirect unauthorized users cleanly
show “Access denied” page
make /dashboard choose page based on exact user role + store scope
prevent Store Manager from seeing company-wide data
4. Stage 7F — Real dashboard analytics

Current dashboards have core KPIs. Still left:

daily/monthly sales charts
purchase charts
profit charts
GST vs Non-GST split
stock ageing
low-stock risk dashboard
vendor/customer due dashboard
cash/payment summary
store-group comparison charts
export dashboard data to CSV/PDF
5. Stage 7G — Nuxt UI component cleanup

Still left:

replace more custom cards/tables with Nuxt UI components
standardize page headers
standardize forms
standardize modals/slideover
standardize filter panels
make all pages visually consistent with dashboard shell
6. Stage 7H — Documentation and revert safety

Still left:

document all new dashboard routes
document revert process
keep old shell backup
keep changelog/version history
add “current running version” visible in About page/sidebar
add a simple admin “System Info” page showing frontend/backend version match
My recommended next step

request :ok move to next part and tell what will be next part after that , what else is left
Done. I moved to Stage 7J / Version 3.9.0 — Dashboard Export, Print and Snapshot Tools.

Download package: Garmetix-Stage7J-DashboardExportPrint-v3.9.zip

Notes: Stage7J-Dashboard-Export-Print-Notes.md
Validation log: Stage7J-Validation-Log.md
Stage 7 TODO: Stage7-TODO.md
Implementation map: Stage7-Implementation-Map.md

Implemented:

Added reusable component:
components/dashboard/ExportActions.vue
Added export tools to:
/dashboard/store-manager
/dashboard/business
Added dashboard snapshot export:
JSON
CSV
Print / PDF through browser print
Added cleaner print CSS for dashboard pages.
Updated version identity:
Version: 3.9.0
Stage: Stage 7J
Build Code: GARMETIX-7J-20260610-390

Validation completed:

Static validation passed.
Vue structural checks passed.
ZIP integrity check passed.

Not run here:

dotnet build, because .NET SDK is not installed in this sandbox.
Docker build, because Docker is unavailable here.
Full npm run build, because dependencies are not installed here.

Run locally:

cd backend
dotnet build

cd ../frontend/garmetix-web
npm ci
npm run build

cd ../..
docker compose up --build

Test:

/dashboard/store-manager
/dashboard/business
/about-us
/api/app-info/version
Next part after this

Recommended next: Stage 7K / Version 3.10.0 — Dashboard Filters, Preferences and Auto Refresh

That should add:

dashboard date-range filter
Today / 7 days / 30 days / custom period
auto-refresh interval
refresh/pause control
save dashboard preferences locally
remember dashboard scope and selected widgets
show “last refreshed at”
better error/retry state for dashboard widgets
What is still left

After Stage 7J, the main remaining items are:

Local build/runtime check from your side with Docker logs.
Stage 7K dashboard filters and preferences.
Stage 7L chart improvements for sales, purchase, profit, stock, GST vs Non-GST.
Stage 7M convert more old pages to Nuxt UI dashboard-style cards, tables, modals and filters.
Stage 8 business feature expansion after dashboard layout is stable.




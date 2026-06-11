export const APP_VERSION = '3.11.0'
export const APP_STAGE = 'Stage 7L'
export const APP_RELEASE_NAME = 'Dashboard Charts and UI Layout Audit'
export const APP_BUILD_DATE = '2026-06-10'
export const APP_BUILD_CODE = 'GARMETIX-7L-20260610-3110'

export const APP_HIGHLIGHTS = [
  'Stage 7L adds richer dashboard chart breakdowns and a UI layout audit page for spacing, padding and overlap review',
  'Stage 7K adds dashboard date-range filters, saved browser preferences and auto-refresh controls',
  'Stage 7J adds dashboard JSON export, CSV export and print/PDF snapshot tools',
  'Stage 7I refactors dashboard widgets into reusable Nuxt UI components with shared empty, loading, metric, chart, list and table states',
  'Stage 7H adds System Info, version match checks, route audit and safer dashboard rollback visibility',
  'Stage 7G adds central permission-aware menu filtering, page guards and Access Denied routing',
  'Stage 7F removes duplicate Help, Account, and Workspace entries from the main sidebar while keeping them in footer/status menus',
  'Stage 7D adds controlled collapsible sidebar, icon-only mode and footer account menu',
  'Stage 7C adds dashboard breadcrumbs, favorites, recent pages and Ctrl/Cmd+K command memory',
  'Dashboard Map page documents Stage 7 routing, preserved menu groups, version identity and revert policy',
  'Sidebar/topbar UX polished while keeping all current pages and legacy shell rollback',
  'Stage 7B role-aware /dashboard landing redirects users to the correct dashboard',
  'Dashboard shell now adds a smart dashboard menu item and topbar shortcut',
  'Store manager dashboard now includes quick actions and daily health signals',
  'Business dashboard now includes executive actions, health signals and store-group performance',
  'Dashboard backend adds /api/dashboard/home and deeper role/workspace scoped KPIs',
  'Nuxt UI dashboard-template style shell with collapsible sidebar, topbar, command search and dark-mode polish',
  'Store Manager dashboard scoped to the logged-in user current store',
  'Owner/Admin/Accountant dashboard with company, store-group and store-wise KPIs',
  'All existing pages and menus preserved; legacy shell can be restored with NUXT_PUBLIC_DASHBOARD_SHELL=legacy',
  'Stage 7 TODO and implementation map added for safe two-part dashboard migration',
  'Modern login screen with one primary Login button and compact forgot/reset links',
  'Global auth guard redirects expired or missing sessions to Login before protected pages render',
  'API 401 handling now clears expired sessions and returns the user to Login',
  'My Profile page with editable name, username, email and self-service password change',
  'Profile changes never allow users to modify their own role, permission or workspace assignment',
  'Company, store group and store onboarding module',
  'AF/SS default data seeder module',
  'Application message logs with filters',
  'Version identity surfaced in About Us and API',
  'Non-GST/out-of-scope goods purchase and sale module',
  'Buildfix: added missing Store namespace import for Docker publish',
  'Compile fix: resolved stock variable shadowing and ProductCategory ambiguity',
  'Multi-item Non-GST purchase memo and sale cash memo with print view',
  'Non-GST sale, purchase, profit and current stock reports',
  'Separate Non-GST stock flag and movement ledger',
  'Separate Non-GST goods reports and visible accounting postings',
  'About Us, Contact Us and FAQ pages',
  'Runtime fix: removed duplicate app-info and message-log root routes',
  'Runtime fix: Message Logs PostgreSQL filter query no longer uses untyped NULL parameters',
  'Runtime fix: Non-GST sale sends null stock id instead of blank string and purchase auto-generates barcode when blank'
]

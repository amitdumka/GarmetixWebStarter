# Stage 12G.1 - Main Back Office Route Shell

Version: 5.12.27

## Scope

This stage begins Main Back Office cleanup inside `modular/apps/main` without changing legacy behavior or backend/database code.

## Current Finding

The modular Main app was still a single `app.vue` shell with no `pages/` directory. That meant Back Office routes were displayed as route registry rows instead of being split into actual route chunks.

## Added

- Authenticated Main shell with the same token/session pattern used by POS, HR, Books and Admin.
- `MainPlaceholder` component for safe route shells.
- Main route pages for:
  - `/`
  - `/dashboard`
  - `/dashboard/todays`
  - `/dashboard/store-manager`
  - `/dashboard/map`
  - `/store-day`
  - `/billing`
  - `/tailoring`
  - `/purchase`
  - `/purchase/new`
  - `/purchase-return`
  - `/inventory`
  - `/stock-operations`
  - `/customers`
  - `/customers/new`
  - `/customers/:id`
  - `/reports`
  - `/document-scan`
  - `/profile`
  - `/about-us`
  - `/contact-us`
  - `/faq`
  - fallback `/:module`

## Route Ownership Notes

Main Back Office should keep:

- Dashboards and store operations.
- Sale invoice list/review, while POS keeps fast sale entry and returns.
- Purchase and purchase return operations.
- Inventory and stock operations.
- Customers and operational reports.
- Profile/help pages.

Main Back Office should not reabsorb:

- POS counter flows.
- HR attendance/payroll.
- AI Sense analytics.
- Books/accounting/GST.
- Admin/SaaS owner controls.

## Safety Notes

All pages added in this stage are lightweight placeholders. No write endpoints were connected, no legacy routes were deleted, and no database change was made.

## Validation

Run:

```powershell
npm.cmd run modular:check
npm.cmd --prefix modular run build:main
npm.cmd run legacy:api:build
```

## Next Step

Stage 12G.2 should connect the first read-only Main dashboard data, starting with safe summary endpoints only. Good candidates are store-day summary, sale invoice list summary, product/inventory summary, and customer count/search preview.

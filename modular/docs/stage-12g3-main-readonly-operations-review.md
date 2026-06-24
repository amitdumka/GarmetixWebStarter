# Stage 12G.3 - Main Read-Only Operations Review

Version: 5.12.29

## Scope

This stage connects additional modular Main Back Office pages to existing safe read-only operational endpoints.

## Connected Endpoints

- `GET /api/billing/sales/recent`
- `GET /api/purchase/invoices/recent`
- `GET /api/inventory/stock-reports/summary`
- `GET /api/billing/customers/search`
- `GET /api/dashboard/business`

## Added

- `modular/apps/main/components/MainReadOnlyTable.vue`
- Sale invoice review data on `/billing`
- Purchase invoice review data on `/purchase`
- Inventory stock summary and risk rows on `/inventory`
- Stock operation watch rows on `/stock-operations`
- Customer preview rows on `/customers`
- Operational report overview on `/reports`

## Safety Notes

No write endpoints were connected. This stage only uses authenticated GET endpoints and does not change backend behavior, database schema, or legacy routes.

## Validation

Run:

```powershell
npm.cmd run modular:check
npm.cmd --prefix modular run build:main
npm.cmd run legacy:api:build
```

## Next Step

Stage 12G.4 should add the Main static deployment script for `garmetix.aadwikafashion.in`, matching the POS, HR, AI Sense, Books and Admin release-symlink deploy pattern.

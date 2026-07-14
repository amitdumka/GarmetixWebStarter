# Stage 11D-92 — Vyapar Sale Import Build/Layout Fix — v4.12.07

Base: v4.12.06 Stage 11D-91 Vyapar Sale Import Runtime Validation.

## Purpose

This stage fixes the deploy blocker reported during Docker API publish and restores the Vyapar sale import pages to the standard Garmetix shell layout.

## Fixes

- Fixed API build error in `backend/Garmetix.Api/Billing/BillingEndpoints.cs` where `ToRecentInvoiceDto(...)` referenced `invoice.Remarks` outside invoice scope.
- The DTO helper now uses its `remarks` parameter, so sale invoice remarks still flow to sale list/imported list without compile failure.
- Wrapped `frontend/garmetix-web/pages/billing/vyapar-import.vue` in `AppShell` so it opens with sidebar/navigation like other modules.
- Wrapped `frontend/garmetix-web/pages/billing/vyapar-imported.vue` in `AppShell` for consistent imported invoice history navigation.
- Updated app version metadata to v4.12.07 / Stage 11D-92.

## No schema changes

No database migration was added in this stage. It keeps the v4.12.05 `SalesInvoices.Remarks` migration intact.

## Deploy validation

Run from project root:

```bash
python3 scripts/validation/stage11d92-vyapar-sale-import-build-layout-fix-check.py
docker compose build --no-cache
docker compose up -d
```

## Next part

Stage 11D-93 should implement Vyapar Import Batch Undo + Bulk Barcode Mapping Upload:

- import batch history
- admin-only batch undo/reversal
- bulk barcode mapping upload from Excel/CSV
- mismatch workbook export from import page
- final approval screen before importing large Vyapar sale batches

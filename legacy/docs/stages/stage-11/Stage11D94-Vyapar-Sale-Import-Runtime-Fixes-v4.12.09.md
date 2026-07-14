# Stage 11D-94 — Vyapar Sale Import Runtime Fixes — v4.12.09

Base: v4.12.08 Stage 11D-93 Vyapar Import Batch Undo + Barcode Mapping.

## Problem fixed

After v4.12.05 added `SalesInvoices.Remarks`, deployed databases with old Docker volumes could still miss the physical column when AutoMigrate was disabled or migration history drifted. This caused PostgreSQL error `42703: column s.Remarks does not exist` on:

- `GET /api/billing/sales`
- `GET /api/billing/sales/recent`
- `GET /api/invoice-replacements/pending`
- `GET /api/sale-import/vyapar/imported`

## Fix

Added an idempotent startup repair in `DatabaseSchemaRepairService`:

```sql
ALTER TABLE "SalesInvoices" ADD COLUMN IF NOT EXISTS "Remarks" text NULL;
```

Also added a direct repair SQL and runtime scripts so an already deployed server can be fixed immediately.

## Files changed/added

- `backend/Garmetix.Api/Database/DatabaseSchemaRepairService.cs`
- `backend/Garmetix.Infrastructure/Data/Migrations/GarmetixDbContextModelSnapshot.cs`
- `scripts/production/sql/stage11d94-sales-invoices-remarks-repair.sql`
- `scripts/runtime/stage11d94-repair-sales-invoice-remarks.sh`
- `scripts/runtime/stage11d94-runtime-validation.sh`
- `scripts/validation/stage11d94-vyapar-sale-import-runtime-fixes-check.py`

## Manual quick repair on server

From project root:

```bash
./scripts/runtime/stage11d94-repair-sales-invoice-remarks.sh /opt/garmetix/current
```

## Deploy validation

```bash
python3 scripts/validation/stage11d94-vyapar-sale-import-runtime-fixes-check.py
docker compose build --no-cache
docker compose up -d
./scripts/runtime/stage11d94-runtime-validation.sh --smoke /opt/garmetix/current
```

## UI layout check

The Vyapar Sale Import pages remain wrapped inside `AppShell`, so they should open with the normal sidebar/navigation:

- `/billing/vyapar-import`
- `/billing/vyapar-imported`
- `/billing/vyapar-import-batches`

## Next part

Stage 11D-95 — Vyapar Import Live File Test Fixes + Customer/Payment Mapping QA.

# Stage 5E — EF Migration Consolidation Notes

## Goal

Stage 5E converts the Stage 3A through Stage 5D schema hardening work into a formal EF migration path so Docker/production startup does not depend only on runtime schema repair.

## Added

- `backend/Garmetix.Infrastructure/Data/Migrations/20260609173500_ConsolidateStage3To5Schema.cs`
- `backend/Garmetix.Api/Database/DatabaseMigrationEndpoints.cs`
- `scripts/validation/stage5e-static-checks.py`

## Migration behavior

The consolidated migration is intentionally idempotent and PostgreSQL-safe. It uses `CREATE TABLE IF NOT EXISTS`, `ALTER TABLE IF EXISTS ... ADD COLUMN IF NOT EXISTS`, and `CREATE INDEX IF NOT EXISTS` because existing Docker volumes may already contain many of these objects from runtime repair checks introduced in earlier stages.

The migration consolidates these areas:

- GST verification fields on customers/vendors
- Salesmen table
- Product master Stage 3A fields/tables
- Purchase invoice store/date columns
- Invoice item and purchase item product/HSN/unit/GST split snapshots
- Sales payment detail fields
- Card payment detail fields
- Vendor payment purchase-link fields
- PurchasePayments table
- StockMovements table
- DocumentSequences table
- related indexes for product, purchase, payment, stock movement, and document sequence lookup

## Pending model warning handling

`DependencyInjection.cs` now ignores `RelationalEventId.PendingModelChangesWarning` at runtime because this project uses hand-written/idempotent migrations during this stabilization phase. Without this, EF can restart-loop Docker startup even when the actual database is already schema-compatible.

Schema health is now checked through:

- EF migration status endpoint
- existing data consistency dashboard
- runtime schema repair endpoint for old volumes

## New admin endpoint

`GET /api/database/migrations/status`

Returns:

- applied migration count
- pending migration count
- applied migration list
- pending migration list
- `Database:AutoMigrate` status
- last known consolidated migration id

## Existing runtime repair

`DatabaseSchemaRepairService` remains in place intentionally. It is still useful for old development volumes that may have partial schema drift. The formal migration should handle clean installs and normal upgrades; runtime repair is a safety net.

## Local verification

Run:

```bash
python scripts/validation/stage5e-static-checks.py
cd backend
dotnet build
cd ..
docker compose up --build
```

After login as admin, call:

```text
/api/database/migrations/status
```

Expected after first startup with `Database:AutoMigrate=true`:

- `PendingCount` should become `0`
- `Applied` should include `20260609173500_ConsolidateStage3To5Schema`

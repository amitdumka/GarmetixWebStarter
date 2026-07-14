# Stage 8I Package 3 - Portable JSON Seeder + Default Accounting Safety (v4.9.2)

This package implements the crash-recovery/system-migration seeder requirement.

## Portable JSON Seeder

New admin-only API:

- `GET /api/portable-seeder/export`
- `POST /api/portable-seeder/import`

The export produces a `garmetix-portable-seeder-*.json` file from current database tables. Import upserts rows by `Id` where available.

Use cases:

- Move from crashed Mac mini/server to another system.
- Keep a JSON seeder snapshot together with database backups.
- Seed a fresh system with real working data when required.

## AF/SS Seeder Change

AF/SS seeder no longer requires an existing company first.

- Default mode creates a new company/store group/store from selected profile.
- Existing company update is still available by turning off "create company automatically".
- Seeder remains idempotent and reuses/upserts default rows.

## Default Indian Accounting Safety

- Company creation now auto-seeds default Indian accounting ledger groups/ledgers.
- Default ledger groups/ledgers are protected from delete.
- Existing default rows are marked with `SystemDefaultAccounting` when defaults are seeded.
- Default rows are updated/reused if already present.

## Cascade Soft Delete

Generic delete for company/store group/store now applies cascade soft delete to child rows with matching scope columns.

## Admin Only

Portable seeder, AF/SS seeder and protected operations are Admin policy guarded.

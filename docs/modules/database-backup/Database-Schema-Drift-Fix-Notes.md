# Database Schema Drift Fix

## Problem

Existing development PostgreSQL volumes can sometimes contain older table shapes even when the code has newer entity fields. The observed failure was:

- `column c.GSTLegalName does not exist`
- `column v.GSTLegalName does not exist`

This affected `/api/customers` and `/api/vendors` after the GSTIN verification feature was added.

## Fix

Startup now runs an idempotent schema drift repair after EF migrations when `Database:AutoMigrate=true`.

It adds missing Customer/Vendor GSTIN verification columns and supporting indexes using PostgreSQL `ADD COLUMN IF NOT EXISTS` / `CREATE INDEX IF NOT EXISTS`.

This is safe for development and existing local Docker volumes. It does not drop or overwrite data.

## Manual alternative

If auto-migration is disabled, run:

```bash
dotnet ef database update --project backend/Garmetix.Infrastructure --startup-project backend/Garmetix.Api
```

Then restart Docker.

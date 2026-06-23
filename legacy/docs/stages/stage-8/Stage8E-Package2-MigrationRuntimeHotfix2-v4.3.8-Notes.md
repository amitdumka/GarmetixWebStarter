# Stage 8E Package 2 Migration Runtime Hotfix 2 / v4.3.8

Date: 2026-06-16

## Problem

Mac mini Docker deployment reached the Nuxt login page, but the API container restarted during EF Core startup migrations.

Observed production errors:

- `42P01: relation "PurchasePayments" does not exist` while applying `20260615061000_AddVendorSettlements`.
- Nuxt runtime icon route failed with `ERR_MODULE_NOT_FOUND: Cannot find package '@iconify-json/lucide'`.

## Fix

- Hardened `20260615061000_AddVendorSettlements` so upgraded databases that have the migration history but are missing the legacy `PurchasePayments` table create it idempotently before new columns/indexes are applied.
- Changed `PurchasePayments` down migration statements to use `ALTER TABLE IF EXISTS`.
- Guarded the vendor-settlement backfill update behind `to_regclass` checks for `PurchaseReturns` and `CommercialNotes`.
- Updated the frontend production Docker image to install `@iconify-json/lucide` locally, so Nuxt UI icon rendering no longer depends on external Iconify calls and no longer crashes when the package is absent from the standalone output.

## Validation

- Static migration guard checks added.
- Existing Stage 8E validation suite still passes.

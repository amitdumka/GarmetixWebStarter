#!/usr/bin/env python3
from pathlib import Path

root = Path(__file__).resolve().parents[2]
errors: list[str] = []

migration = root / 'backend/Garmetix.Infrastructure/Data/Migrations/20260615061000_AddVendorSettlements.cs'
text = migration.read_text()
required = [
    'CREATE TABLE IF NOT EXISTS "PurchasePayments"',
    'ALTER TABLE IF EXISTS "PurchasePayments" ADD COLUMN IF NOT EXISTS "AdjustmentSourceType"',
    'ALTER TABLE IF EXISTS "PurchasePayments" ADD COLUMN IF NOT EXISTS "AdjustmentSourceId"',
    "IF to_regclass('\"PurchaseReturns\"') IS NOT NULL",
    "AND to_regclass('\"CommercialNotes\"') IS NOT NULL THEN",
    'ALTER TABLE IF EXISTS "PurchasePayments" DROP COLUMN IF EXISTS "AdjustmentSourceId"',
]
for item in required:
    if item not in text:
        errors.append(f'Missing vendor settlement migration hardening marker: {item}')

for forbidden in [
    'ALTER TABLE "PurchasePayments" ADD COLUMN',
    'ALTER TABLE "PurchasePayments" DROP COLUMN',
]:
    if forbidden in text:
        errors.append(f'Unsafe non-IF EXISTS PurchasePayments migration statement remains: {forbidden}')

web_dockerfile = (root / 'frontend/garmetix-web/Dockerfile').read_text()
if 'npm install --no-save @iconify-json/lucide' not in web_dockerfile:
    errors.append('Frontend production Dockerfile does not install @iconify-json/lucide for Nuxt icon server bundle runtime.')

notes = root / 'docs/stages/stage-8/Stage8E-Package2-MigrationRuntimeHotfix2-v4.3.8-Notes.md'
if not notes.exists():
    errors.append('Migration runtime hotfix 2 stage notes are missing.')

if errors:
    print('Stage 8E Package 2 migration runtime hotfix 2 validation failed:')
    for error in errors:
        print(f'- {error}')
    raise SystemExit(1)

print('Stage 8E Package 2 migration runtime hotfix 2 static validation passed.')

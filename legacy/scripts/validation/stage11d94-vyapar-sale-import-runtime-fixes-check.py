#!/usr/bin/env python3
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
checks = {
    'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs': ['4.12.09', 'Stage 11D-94 Vyapar Import Runtime Fixes', 'GARMETIX-11D94-20260629-4209'],
    'frontend/garmetix-web/utils/appVersion.ts': ["APP_VERSION = '4.12.09'", 'Stage 11D-94 Vyapar Import Runtime Fixes'],
    'frontend/garmetix-web/package.json': ['"version": "4.12.09"'],
    'backend/Garmetix.Api/Garmetix.Api.csproj': ['<Version>4.12.09</Version>', '4.12.09-vyapar-import-runtime-fixes'],
    'backend/Garmetix.Api/Database/DatabaseSchemaRepairService.cs': ['ALTER TABLE "SalesInvoices" ADD COLUMN IF NOT EXISTS "Remarks" text NULL', 'Stage 11D-94'],
    'backend/Garmetix.Infrastructure/Data/Migrations/GarmetixDbContextModelSnapshot.cs': ['b.Property<string>("Remarks")', 'b.ToTable("SalesInvoices")'],
    'scripts/production/sql/stage11d94-sales-invoices-remarks-repair.sql': ['ALTER TABLE "SalesInvoices" ADD COLUMN IF NOT EXISTS "Remarks" text NULL'],
    'scripts/runtime/stage11d94-repair-sales-invoice-remarks.sh': ['stage11d94-sales-invoices-remarks-repair.sql', 'docker compose'],
    'scripts/runtime/stage11d94-runtime-validation.sh': ['/api/billing/sales?datePreset=today&page=1&pageSize=50', '/api/invoice-replacements/pending?take=150'],
    'frontend/garmetix-web/pages/billing/vyapar-import.vue': ['<AppShell title="Vyapar Sale Import"'],
    'frontend/garmetix-web/pages/billing/vyapar-imported.vue': ['<AppShell title="Imported Vyapar Sales"'],
    'frontend/garmetix-web/pages/billing/vyapar-import-batches.vue': ['<AppShell title="Vyapar Import Batches"'],
}

failed = False
for rel, tokens in checks.items():
    path = root / rel
    if not path.exists():
        print(f'MISSING FILE: {rel}')
        failed = True
        continue
    text = path.read_text(errors='ignore')
    for token in tokens:
        if token not in text:
            print(f'MISSING TOKEN in {rel}: {token}')
            failed = True

if failed:
    print('Stage 11D-94 validation FAILED')
    sys.exit(1)
print('Stage 11D-94 validation passed')

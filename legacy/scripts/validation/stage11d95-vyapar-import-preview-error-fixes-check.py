#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
checks = [
    (ROOT / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs', ['4.12.10', 'Stage 11D-95 Vyapar Import Preview Error Fixes', 'GARMETIX-11D95-20260629-4210']),
    (ROOT / 'frontend/garmetix-web/utils/appVersion.ts', ["APP_VERSION = '4.12.10'", 'Stage 11D-95 Vyapar Import Preview Error Fixes']),
    (ROOT / 'frontend/garmetix-web/package.json', ['"version": "4.12.10"']),
    (ROOT / 'backend/Garmetix.Api/Garmetix.Api.csproj', ['<Version>4.12.10</Version>', '4.12.10-vyapar-import-preview-error-fixes']),
    (ROOT / 'frontend/garmetix-web/composables/useUiFeedback.ts', ['function error(', 'looksLikeFetchError', 'error,\n    errorMessage']),
    (ROOT / 'backend/Garmetix.Api/SaleImport/VyaparSaleImportService.cs', ['EnsureSalesInvoiceRemarksColumnAsync', 'ALTER TABLE "SalesInvoices" ADD COLUMN IF NOT EXISTS "Remarks" text NULL']),
]
failed = False
for path, tokens in checks:
    if not path.exists():
        print(f'MISSING FILE: {path.relative_to(ROOT)}')
        failed = True
        continue
    text = path.read_text()
    for token in tokens:
        if token not in text:
            print(f'MISSING TOKEN in {path.relative_to(ROOT)}: {token}')
            failed = True
if failed:
    print('Stage 11D-95 validation FAILED')
    sys.exit(1)
print('Stage 11D-95 validation passed')

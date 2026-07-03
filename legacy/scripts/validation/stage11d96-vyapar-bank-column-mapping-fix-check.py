#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
checks = [
    (ROOT / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs', ['4.12.11', 'Stage 11D-96 Vyapar Bank Column Mapping Fix', 'GARMETIX-11D96-20260629-4211']),
    (ROOT / 'frontend/garmetix-web/utils/appVersion.ts', ["APP_VERSION = '4.12.11'", 'Stage 11D-96 Vyapar Bank Column Mapping Fix']),
    (ROOT / 'frontend/garmetix-web/package.json', ['"version": "4.12.11"']),
    (ROOT / 'backend/Garmetix.Api/Garmetix.Api.csproj', ['<Version>4.12.11</Version>', '4.12.11-vyapar-bank-column-mapping-fix']),
    (ROOT / 'backend/Garmetix.Api/SaleImport/VyaparSaleImportService.cs', ['unmappedNonCashSources', 'Map each Vyapar bank/POS/UPI column', "payment source '{item.SourceName}' is non-cash", '_ = defaultBankAccountId;']),
    (ROOT / 'frontend/garmetix-web/pages/billing/vyapar-import.vue', ['guessBankAccountForSource', 'Select exact bank/POS account', 'unmappedNonCashSources()', 'Fallback bank/account for old files without bank columns']),
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
    print('Stage 11D-96 validation FAILED')
    sys.exit(1)
print('Stage 11D-96 validation passed')

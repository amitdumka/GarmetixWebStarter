#!/usr/bin/env python3
from pathlib import Path
import sys

checks = {
    'backend/Garmetix.Api/SaleImport/VyaparSaleImportService.cs': [
        'GetImportAvailabilitySnapshotAsync',
        'VyaparImportHistoricalStockBridgeIn',
        'Enable \'Bridge insufficient/historical stock\'',
        'StockLedgerCalculator.Replay',
    ],
    'frontend/garmetix-web/pages/billing/vyapar-import.vue': [
        'Bridge insufficient/historical stock',
        'Allow historical stock bridge',
    ],
    'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs': [
        '4.12.14',
        'Stage 11D-99 Vyapar Historical Stock Bridge Fix',
        'GARMETIX-11D99-20260629-4214',
    ],
    'frontend/garmetix-web/utils/appVersion.ts': [
        "APP_VERSION = '4.12.14'",
        'Stage 11D-99 Vyapar Historical Stock Bridge Fix',
    ],
    'backend/Garmetix.Api/Garmetix.Api.csproj': [
        '<Version>4.12.14</Version>',
        '4.12.14-vyapar-historical-stock-bridge-fix',
    ],
}

ok = True
for file, tokens in checks.items():
    p = Path(file)
    if not p.exists():
        print(f'MISSING FILE: {file}')
        ok = False
        continue
    text = p.read_text(errors='ignore')
    for token in tokens:
        if token not in text:
            print(f'MISSING TOKEN in {file}: {token}')
            ok = False

if not ok:
    print('Stage 11D-99 validation FAILED')
    sys.exit(1)
print('Stage 11D-99 validation passed')

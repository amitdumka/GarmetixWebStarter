#!/usr/bin/env python3
from pathlib import Path
import sys
ROOT = Path(__file__).resolve().parents[2]
checks = [
    (ROOT / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs', ['4.12.13', 'Stage 11D-98 Vyapar Import History Clear Fix', 'GARMETIX-11D98-20260629-4213']),
    (ROOT / 'frontend/garmetix-web/utils/appVersion.ts', ["APP_VERSION = '4.12.13'", 'Stage 11D-98 Vyapar Import History Clear Fix']),
    (ROOT / 'frontend/garmetix-web/package.json', ['"version": "4.12.13"']),
    (ROOT / 'backend/Garmetix.Api/Garmetix.Api.csproj', ['<Version>4.12.13</Version>', '4.12.13-vyapar-import-history-clear-fix']),
    (ROOT / 'backend/Garmetix.Api/SaleImport/VyaparSaleImportEndpoints.cs', ['/history/clear', 'ClearImportHistoryAsync']),
    (ROOT / 'backend/Garmetix.Api/SaleImport/VyaparSaleImportDtos.cs', ['VyaparSaleImportClearHistoryRequest', 'VyaparSaleImportClearHistoryResponse']),
    (ROOT / 'backend/Garmetix.Api/SaleImport/VyaparSaleImportService.cs', ['ClearImportHistoryAsync', 'ClearVyaparImportMarkers', 'item.InvoiceStatus != InvoiceStatus.Cancelled', 'VyaparSaleImportHistoryClear']),
    (ROOT / 'frontend/garmetix-web/pages/billing/vyapar-import-batches.vue', ['Clear Cancelled History', 'sale-import/vyapar/history/clear', 'clearCancelledOnly', 'allowActiveHistoryClear']),
    (ROOT / 'frontend/garmetix-web/pages/billing/vyapar-import.vue', ['Import Batches / Clear History', 'Cancelled/undone previous imports will no longer auto-hide']),
    (ROOT / 'docs/stages/stage-11/Stage11D98-Vyapar-Import-History-Clear-Fix-v4.12.13.md', ['Preview no longer auto-hides cancelled', '/api/sale-import/vyapar/history/clear']),
]
missing = []
for path, tokens in checks:
    if not path.exists():
        missing.append(f'MISSING FILE: {path.relative_to(ROOT)}')
        continue
    text = path.read_text(errors='ignore')
    for token in tokens:
        if token not in text:
            missing.append(f'MISSING TOKEN in {path.relative_to(ROOT)}: {token}')
if missing:
    print('\n'.join(missing))
    print('Stage 11D-98 validation FAILED')
    sys.exit(1)
print('Stage 11D-98 validation passed')

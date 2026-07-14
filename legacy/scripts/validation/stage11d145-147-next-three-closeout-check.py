#!/usr/bin/env python3
from pathlib import Path
root = Path(__file__).resolve().parents[2]
required = [
    'backend/Garmetix.Api/Reports/ProfitLossReportEndpoints.cs',
    'backend/Garmetix.Api/Inventory/StockValuationClosureEndpoints.cs',
    'backend/Garmetix.Api/Closeout/OwnerCloseoutCommandCenterEndpoints.cs',
    'frontend/garmetix-web/pages/reports/profit-loss.vue',
    'frontend/garmetix-web/pages/inventory/stock-valuation-closure.vue',
    'frontend/garmetix-web/pages/owner-closeout-command-center/index.vue',
    'docs/stages/stage-11/Stage11D145-Profit-Loss-Invoice-Item-Reporting-v4.12.60.md',
    'docs/stages/stage-11/Stage11D146-Stock-Valuation-Closure-v4.12.61.md',
    'docs/stages/stage-11/Stage11D147-Owner-Closeout-Command-Center-v4.12.62.md'
]
missing = [p for p in required if not (root / p).exists()]
if missing:
    raise SystemExit('Missing files: ' + ', '.join(missing))
checks = {
    'backend/Garmetix.Api/Program.cs': ['MapProfitLossReportEndpoints', 'MapStockValuationClosureEndpoints', 'MapOwnerCloseoutCommandCenterEndpoints'],
    'frontend/garmetix-web/components/AppShell.vue': ['/reports/profit-loss', '/inventory/stock-valuation-closure', '/owner-closeout-command-center'],
    'frontend/garmetix-web/composables/useAccessControl.ts': ['/reports/profit-loss', '/inventory/stock-valuation-closure', '/owner-closeout-command-center'],
    'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs': ['4.12.62', 'Stage 11D-147 Owner Closeout Command Center', 'GARMETIX-11D147-20260703-4262'],
    'frontend/garmetix-web/utils/appVersion.ts': ['4.12.62', 'Stage 11D-147 Owner Closeout Command Center', 'GARMETIX-11D147-20260703-4262']
}
for rel, needles in checks.items():
    text = (root / rel).read_text()
    for needle in needles:
        if needle not in text:
            raise SystemExit(f'{needle!r} missing from {rel}')
print('Stage 11D-145..147 validation passed')

#!/usr/bin/env python3
from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = []

def add(name, ok):
    checks.append((name, bool(ok)))

fy = (root / 'backend/Garmetix.Api/Closeout/FinancialYearCloseoutEndpoints.cs').read_text()
prod = (root / 'backend/Garmetix.Api/Production/ProductionGoLiveMasterAcceptanceEndpoints.cs').read_text()
app = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
front = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()

add('version updated backend', '4.12.68' in app)
add('version updated frontend', '4.12.68' in front)
add('build code updated', 'GARMETIX-11D153-20260703-4268' in app and 'GARMETIX-11D153-20260703-4268' in front)
add('metric dto description default present', 'FinancialYearCloseoutMetricDto(string Label, int Count, decimal? Amount, string Description = "")' in fy)
add('invoice StoreGroupId invalid access removed', 'item.StoreGroupId == storeGroupId.Value || storeIdsForGroup.Contains(item.StoreId)' not in fy)
add('invoice group scope uses store ids', 'query.Where(item => storeIdsForGroup.Contains(item.StoreId))' in fy)
add('purchase invoice Paid invalid access removed', 'PaidAmount = item.Paid' not in prod)
add('purchase payment grouped paid evidence present', 'purchasePaymentsForPeriod' in prod and 'GroupBy(item => item.PurchaseInvoiceId)' in prod)
add('release note exists', (root / 'RELEASE-v4.12.68.txt').exists())

failed = [name for name, ok in checks if not ok]
if failed:
    print('FAILED:')
    for name in failed:
        print(f'- {name}')
    raise SystemExit(1)
print(f'All {len(checks)} Stage 11D-153 validation checks passed.')

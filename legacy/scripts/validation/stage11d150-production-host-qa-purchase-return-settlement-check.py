#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
checks = []

def read(path):
    return (ROOT / path).read_text(errors='ignore')

def add(name, passed, detail=''):
    checks.append((name, bool(passed), detail))

files = {
    'app_info': read('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs'),
    'app_version': read('frontend/garmetix-web/utils/appVersion.ts'),
    'program': read('backend/Garmetix.Api/Program.cs'),
    'purchase_endpoint': read('backend/Garmetix.Api/Purchase/PurchaseReturnAdvancedSettlementEndpoints.cs'),
    'host_endpoint': read('backend/Garmetix.Api/Production/ProductionHostBuildQaEndpoints.cs'),
    'purchase_page': read('frontend/garmetix-web/pages/purchase-return/advanced-settlement.vue'),
    'host_page': read('frontend/garmetix-web/pages/production-host-build-qa/index.vue'),
    'shell': read('frontend/garmetix-web/components/AppShell.vue'),
    'legacy': read('frontend/garmetix-web/components/AppShellLegacy.vue'),
    'access': read('frontend/garmetix-web/composables/useAccessControl.ts'),
    'dtos': read('backend/Garmetix.Api/Purchase/PurchaseDtos.cs'),
}

for key in ['app_info', 'app_version']:
    text = files[key]
    add(f'{key} version', '4.12.65' in text)
    add(f'{key} stage', 'Stage 11D-150 Production Host QA + Purchase Return Settlement' in text)
    add(f'{key} build code', 'GARMETIX-11D150-20260703-4265' in text)

add('program maps purchase return advanced endpoint', 'MapPurchaseReturnAdvancedSettlementEndpoints();' in files['program'])
add('program maps production host endpoint', 'MapProductionHostBuildQaEndpoints();' in files['program'])
add('purchase endpoint route', '/api/purchase-return/advanced-settlement' in files['purchase_endpoint'])
add('purchase endpoint csv', '/evidence.csv' in files['purchase_endpoint'] and 'BuildCsv' in files['purchase_endpoint'])
add('purchase endpoint exact itc', 'ITC_REVERSAL_COMPONENT_MISMATCH' in files['purchase_endpoint'] and 'PURCHASE_RETURN_JOURNAL_ITC_MISMATCH' in files['purchase_endpoint'])
add('purchase endpoint refund proof', 'VENDOR_REFUND_REFERENCE_MISSING' in files['purchase_endpoint'] and 'VENDOR_REFUND_BANK_TRANSACTION_MISSING' in files['purchase_endpoint'])
add('host endpoint route', '/api/production-host-build-qa' in files['host_endpoint'])
add('host endpoint probes purchase return tables', 'PURCHASE_RETURN_ITC_TABLE' in files['host_endpoint'] and 'VENDOR_SETTLEMENT_TABLE' in files['host_endpoint'])
add('purchase page route references api', 'purchase-return/advanced-settlement' in files['purchase_page'])
add('host page route references api', 'production-host-build-qa' in files['host_page'])
add('modern menu links', '/purchase-return/advanced-settlement' in files['shell'] and '/production-host-build-qa' in files['shell'])
add('legacy menu links', '/purchase-return/advanced-settlement' in files['legacy'] and '/production-host-build-qa' in files['legacy'])
add('access control links', '/purchase-return/advanced-settlement' in files['access'] and '/production-host-build-qa' in files['access'])
add('duplicate Printed parameter fixed', '    bool Printed,\n    bool Printed,' not in files['dtos'])
for path in [
    'docs/stages/stage-11/Stage11D150-Production-Host-QA-Purchase-Return-Settlement-v4.12.65.md',
    'docs/modules/purchase-inventory/purchase-return-advanced-settlement-v4.12.65.md',
    'docs/modules/production-go-live/production-host-build-qa-v4.12.65.md',
    'RELEASE-v4.12.65.txt',
]:
    add(f'{path} exists', (ROOT / path).exists())

failed = [item for item in checks if not item[1]]
for name, passed, detail in checks:
    print(f"{'PASS' if passed else 'FAIL'}: {name}{' - ' + detail if detail else ''}")

if failed:
    print(f"\n{len(failed)} validation check(s) failed.", file=sys.stderr)
    sys.exit(1)
print(f"\nAll {len(checks)} Stage 11D-150 validation checks passed.")

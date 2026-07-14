#!/usr/bin/env python3
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
files = {
    'backend': root / 'backend/Garmetix.Api/Billing/BillingFinalQaEndpoints.cs',
    'program': root / 'backend/Garmetix.Api/Program.cs',
    'page': root / 'frontend/garmetix-web/pages/billing/final-qa.vue',
    'app_shell': root / 'frontend/garmetix-web/components/AppShell.vue',
    'legacy_shell': root / 'frontend/garmetix-web/components/AppShellLegacy.vue',
    'access': root / 'frontend/garmetix-web/composables/useAccessControl.ts',
    'billing_index': root / 'frontend/garmetix-web/pages/billing/index.vue',
    'version': root / 'frontend/garmetix-web/utils/appVersion.ts',
    'app_info': root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs',
    'csproj': root / 'backend/Garmetix.Api/Garmetix.Api.csproj',
    'backup': root / 'scripts/linux/create-database-backup-now.sh',
    'smoke': root / 'scripts/linux/smoke-test.sh',
    'doc_stage': root / 'docs/stages/stage-11/Stage11D130-Sale-Billing-Final-QA-Closure-v4.12.45.md',
    'doc_module': root / 'docs/modules/billing-sales/sale-billing-final-qa-closure-v4.12.45.md',
}
missing = [name for name, path in files.items() if not path.exists()]
if missing:
    print('Missing files: ' + ', '.join(missing), file=sys.stderr)
    sys.exit(1)

text = {name: path.read_text(encoding='utf-8') for name, path in files.items()}
checks = []

def add(name, condition):
    checks.append((name, bool(condition)))

add('backend endpoint group', 'MapBillingFinalQaEndpoints' in text['backend'] and '/api/billing/final-qa' in text['backend'])
add('backend json endpoint', 'group.MapGet("", GetFinalQaAsync)' in text['backend'])
add('backend csv endpoint', 'group.MapGet("/evidence.csv", ExportEvidenceCsvAsync)' in text['backend'])
add('backend status', '"Complete"' in text['backend'] and '"Not Complete"' in text['backend'])
add('backend mixed payment checks', 'SALE_MIXED_PAYMENT_WITHOUT_SPLIT_ROWS' in text['backend'] and 'PaymentMode.MixPayments' in text['backend'])
add('backend replacement checks', 'SALE_REPLACEMENT_PENDING_APPROVAL' in text['backend'])
add('backend bank mapping checks', 'SALE_NON_CASH_BANK_MAPPING_MISSING' in text['backend'] and 'RequiresBankMapping' in text['backend'])
add('backend evidence checks', 'SALE_ACCOUNTING_JOURNAL_MISSING' in text['backend'] and 'SALE_STOCK_OUT_MISSING' in text['backend'] and 'SALE_ITEM_BARCODE_MISSING' in text['backend'])
add('program map', 'app.MapBillingFinalQaEndpoints();' in text['program'])
add('frontend page', 'Sale/Billing final QA closure' in text['page'] and 'billing/final-qa/evidence.csv' in text['page'])
add('frontend evidence tables', 'Payment mode reconciliation' in text['page'] and 'Invoice-wise evidence' in text['page'])
add('menus', '/billing/final-qa' in text['app_shell'] and '/billing/final-qa' in text['legacy_shell'])
add('access route', "path: '/billing/final-qa'" in text['access'])
add('billing button', 'to="/billing/final-qa"' in text['billing_index'])
add('frontend version', "APP_VERSION = '4.12.45'" in text['version'] and 'GARMETIX-11D130-20260701-4245' in text['version'])
add('backend version', 'Version = "4.12.45"' in text['app_info'] and 'GARMETIX-11D130-20260701-4245' in text['app_info'])
add('csproj version', '<Version>4.12.45</Version>' in text['csproj'] and 'stage11d130-sale-billing-final-qa-closure' in text['csproj'])
add('backup manifest stage', 'Stage 11D-130 Sale Billing Final QA Closure' in text['backup'])
add('smoke expected version', '4.12.45' in text['smoke'] and 'GARMETIX-11D130-20260701-4245' in text['smoke'])
add('stage doc', 'Stage 11D-130' in text['doc_stage'] and 'GET /api/billing/final-qa' in text['doc_stage'])
add('module doc', 'Sale/Billing Final QA Closure' in text['doc_module'])

failed = [name for name, ok in checks if not ok]
if failed:
    print('Failed checks:')
    for name in failed:
        print(f'- {name}')
    sys.exit(1)
print('Stage 11D-130 Sale/Billing final QA closure static checks passed.')

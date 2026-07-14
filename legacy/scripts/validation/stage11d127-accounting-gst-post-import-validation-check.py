#!/usr/bin/env python3
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
files = {
    'endpoint': root / 'backend/Garmetix.Api/Validation/PostImportLiveValidationEndpoints.cs',
    'program': root / 'backend/Garmetix.Api/Program.cs',
    'page': root / 'frontend/garmetix-web/pages/accounting-gst-validation/index.vue',
    'shell': root / 'frontend/garmetix-web/components/AppShell.vue',
    'legacy': root / 'frontend/garmetix-web/components/AppShellLegacy.vue',
    'app_info': root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs',
    'version': root / 'frontend/garmetix-web/utils/appVersion.ts',
    'csproj': root / 'backend/Garmetix.Api/Garmetix.Api.csproj',
    'backup': root / 'scripts/linux/create-database-backup-now.sh',
    'doc': root / 'docs/stages/stage-11/Stage11D127-Accounting-GST-Post-Import-Validation-v4.12.42.md',
}

missing = [str(path) for path in files.values() if not path.exists()]
if missing:
    print('Missing expected files:')
    print('\n'.join(missing))
    sys.exit(1)

text = {name: path.read_text(encoding='utf-8') for name, path in files.items()}
checks = []

def add(name, ok):
    checks.append((name, bool(ok)))

add('endpoint route present', '/api/post-import-validation' in text['endpoint'] and '/accounting-gst' in text['endpoint'])
add('csv endpoint present', '/accounting-gst.csv' in text['endpoint'] and 'Results.File' in text['endpoint'])
add('endpoint mapped', 'MapPostImportLiveValidationEndpoints' in text['program'])
add('frontend page present', 'Accounting/GST post-import live validation' in text['page'] and 'post-import-validation/accounting-gst' in text['page'])
add('csv export button', 'CSV evidence' in text['page'] and 'accounting-gst.csv' in text['page'])
add('gst/payment tables present', 'GST snapshot' in text['page'] and 'Payment reconciliation' in text['page'])
add('menu modern', '/accounting-gst-validation' in text['shell'])
add('menu legacy', '/accounting-gst-validation' in text['legacy'])
add('version frontend', "APP_VERSION = '4.12.42'" in text['version'] and 'GARMETIX-11D127-20260701-4242' in text['version'])
add('version backend', 'Version = "4.12.42"' in text['app_info'] and 'GARMETIX-11D127-20260701-4242' in text['app_info'])
add('csproj version', '<Version>4.12.42</Version>' in text['csproj'] and 'stage11d127-accounting-gst-post-import-validation' in text['csproj'])
add('backup manifest stage', 'Stage 11D-127 Accounting GST Post-Import Validation' in text['backup'])
add('docs present', 'Accounting/GST Post-Import Live Validation - v4.12.42' in text['doc'])
add('stock and journal checks', 'SALE_ACCOUNTING_JOURNALS' in text['endpoint'] and 'PURCHASE_STOCK_IN' in text['endpoint'])
add('purchase import acceptance check', 'PURCHASE_IMPORT_ACCEPTANCE' in text['endpoint'])

failed = [name for name, ok in checks if not ok]
if failed:
    print('FAILED static checks:')
    for name in failed:
        print(f'- {name}')
    sys.exit(1)

print('Stage 11D-127 Accounting/GST post-import validation static checks passed.')

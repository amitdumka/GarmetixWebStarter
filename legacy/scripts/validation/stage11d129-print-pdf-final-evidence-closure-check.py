#!/usr/bin/env python3
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
files = {
    'backend': root / 'backend/Garmetix.Api/Production/PrintAcceptanceEndpoints.cs',
    'page': root / 'frontend/garmetix-web/pages/print-final-acceptance/index.vue',
    'version': root / 'frontend/garmetix-web/utils/appVersion.ts',
    'app_info': root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs',
    'csproj': root / 'backend/Garmetix.Api/Garmetix.Api.csproj',
    'backup': root / 'scripts/linux/create-database-backup-now.sh',
    'smoke': root / 'scripts/linux/smoke-test.sh',
    'doc_stage': root / 'docs/stages/stage-11/Stage11D129-Print-PDF-Final-Evidence-Closure-v4.12.44.md',
    'doc_module': root / 'docs/modules/reports-audit/print-pdf-final-evidence-closure-v4.12.44.md',
}
missing = [name for name, path in files.items() if not path.exists()]
if missing:
    print('Missing files: ' + ', '.join(missing), file=sys.stderr)
    sys.exit(1)

text = {name: path.read_text(encoding='utf-8') for name, path in files.items()}
checks = []

def add(name, condition):
    checks.append((name, bool(condition)))

add('closure endpoint', 'MapGet("/closure", ClosureAsync)' in text['backend'])
add('csv endpoint', 'MapGet("/evidence.csv", ExportEvidenceCsvAsync)' in text['backend'])
add('closure dto', 'PrintAcceptanceClosureDto' in text['backend'])
add('complete status', '"Complete"' in text['backend'] and '"Not Complete"' in text['backend'])
add('core samples', 'CoreSampleKeys' in text['backend'] and 'salesInvoice' in text['backend'] and 'purchaseInward' in text['backend'])
add('csv bytes helper', 'CsvBytes' in text['backend'] and 'garmetix-print-final-acceptance' in text['backend'])
add('frontend closure card', 'Final print/PDF closure' in text['page'] and 'closureBlockingIssues' in text['page'])
add('frontend csv export', 'downloadEvidenceCsv' in text['page'] and '/print-acceptance/evidence.csv' in text['page'])
add('frontend operator rules', 'Operator rules' in text['page'] and 'Known limitations' in text['page'])
add('frontend version', "APP_VERSION = '4.12.44'" in text['version'] and 'GARMETIX-11D129-20260701-4244' in text['version'])
add('backend version', 'Version = "4.12.44"' in text['app_info'] and 'GARMETIX-11D129-20260701-4244' in text['app_info'])
add('csproj version', '<Version>4.12.44</Version>' in text['csproj'] and 'stage11d129-print-pdf-final-evidence-closure' in text['csproj'])
add('backup manifest stage', 'Stage 11D-129 Print PDF Final Evidence Closure' in text['backup'])
add('smoke expected version', '4.12.44' in text['smoke'] and 'GARMETIX-11D129-20260701-4244' in text['smoke'])
add('stage doc', 'Stage 11D-129' in text['doc_stage'] and 'GET /api/print-acceptance/closure' in text['doc_stage'])
add('module doc', 'Print/PDF Final Evidence Closure' in text['doc_module'])

failed = [name for name, ok in checks if not ok]
if failed:
    print('Failed checks:')
    for name in failed:
        print(f'- {name}')
    sys.exit(1)
print('Stage 11D-129 Print/PDF final evidence closure static checks passed.')

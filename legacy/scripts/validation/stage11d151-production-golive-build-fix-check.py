#!/usr/bin/env python3
from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = []

def add(name, ok):
    checks.append((name, ok))

source = root / 'backend/Garmetix.Api/Production/ProductionGoLiveMasterAcceptanceEndpoints.cs'
text = source.read_text()
add('build fix source exists', source.exists())
add('BuildPrintHtml uses StringBuilder', 'var html = new StringBuilder();' in text)
add('old raw interpolated return removed', 'return $"""' not in text[text.find('private static string BuildPrintHtml'):text.find('private static string Csv')])
add('print button remains', 'Print / Save PDF' in text)
add('app version updated', '4.12.66' in (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text())
add('build code updated', 'GARMETIX-11D151-20260703-4266' in (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text())
add('release note exists', (root / 'RELEASE-v4.12.66.txt').exists())

failed = [name for name, ok in checks if not ok]
if failed:
    print('Failed checks:')
    for name in failed:
        print('-', name)
    raise SystemExit(1)

print(f'All {len(checks)} Stage 11D-151 validation checks passed.')

#!/usr/bin/env python3
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
checks = []

def add(name, ok):
    checks.append((name, bool(ok)))

files = {
    'day_book': root / 'backend/Garmetix.Api/DayBook/DayBookEndpoints.cs',
    'day_page': root / 'frontend/garmetix-web/pages/day-book/index.vue',
    'version': root / 'frontend/garmetix-web/utils/appVersion.ts',
    'app_info': root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs',
    'csproj': root / 'backend/Garmetix.Api/Garmetix.Api.csproj',
    'backup': root / 'scripts/linux/create-database-backup-now.sh',
    'doc': root / 'docs/stages/stage-11/Stage11D128-Day-Book-Final-Export-Source-Polish-v4.12.43.md',
}
text = {name: path.read_text() if path.exists() else '' for name, path in files.items()}

add('day book csv endpoint', 'group.MapGet("/export.csv", ExportCsvAsync)' in text['day_book'])
add('day book print endpoint', 'group.MapGet("/print", PrintAsync)' in text['day_book'])
add('export limit', 'DayBookExportLimit = 5000' in text['day_book'])
add('csv includes source path', 'SourcePath' in text['day_book'] and 'CsvBytes' in text['day_book'])
add('print view', 'Print / Save PDF' in text['day_book'] and 'text/html; charset=utf-8' in text['day_book'])
add('frontend export card', 'Final export/source opening polish' in text['day_page'])
add('frontend csv action', 'downloadDayBookCsv' in text['day_page'] and '/day-book/export.csv?' in text['day_page'])
add('frontend print action', 'openDayBookPrint' in text['day_page'] and '/day-book/print?' in text['day_page'])
add('source opening hints', 'dayBookSourceId' in text['day_page'] and 'dayBookSourceType' in text['day_page'] and 'dayBookDate' in text['day_page'])
add('frontend version', "APP_VERSION = '4.12.43'" in text['version'] and 'GARMETIX-11D128-20260701-4243' in text['version'])
add('backend version', 'Version = "4.12.43"' in text['app_info'] and 'GARMETIX-11D128-20260701-4243' in text['app_info'])
add('csproj version', '<Version>4.12.43</Version>' in text['csproj'] and 'stage11d128-day-book-final-export-source-polish' in text['csproj'])
add('backup manifest stage', 'Stage 11D-128 Day Book Final Export Source Polish' in text['backup'])
add('docs present', 'Day Book Final Export + Source Opening Polish' in text['doc'])

failed = [name for name, ok in checks if not ok]
if failed:
    for name in failed:
        print(f'FAILED: {name}', file=sys.stderr)
    sys.exit(1)

print('Stage 11D-128 Day Book final export/source polish static checks passed.')

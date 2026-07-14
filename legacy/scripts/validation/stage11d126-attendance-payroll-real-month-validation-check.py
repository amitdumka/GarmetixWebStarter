#!/usr/bin/env python3
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
paths = {
    'endpoint': root / 'backend/Garmetix.Api/Payroll/PayrollEndpoints.cs',
    'dtos': root / 'backend/Garmetix.Api/Payroll/PayrollDtos.cs',
    'page': root / 'frontend/garmetix-web/pages/payroll/finalization.vue',
    'version': root / 'frontend/garmetix-web/utils/appVersion.ts',
    'app_info': root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs',
    'csproj': root / 'backend/Garmetix.Api/Garmetix.Api.csproj',
    'backup': root / 'scripts/linux/create-database-backup-now.sh',
    'doc': root / 'docs/stages/stage-11/Stage11D126-Attendance-Payroll-Real-Month-Validation-v4.12.41.md',
}
missing = [name for name, path in paths.items() if not path.exists()]
if missing:
    print('Missing files: ' + ', '.join(missing), file=sys.stderr)
    sys.exit(1)

text = {name: path.read_text(encoding='utf-8') for name, path in paths.items()}
checks: list[tuple[str, bool]] = []

def add(name: str, ok: bool):
    checks.append((name, ok))

add('version v4.12.41 frontend', "APP_VERSION = '4.12.41'" in text['version'] and 'GARMETIX-11D126-20260701-4241' in text['version'])
add('version v4.12.41 backend', 'Version = "4.12.41"' in text['app_info'] and 'GARMETIX-11D126-20260701-4241' in text['app_info'])
add('csproj version', '<Version>4.12.41</Version>' in text['csproj'] and 'stage11d126-attendance-payroll-real-month-validation' in text['csproj'])
add('endpoint mapped', 'group.MapGet("/real-month-validation", RealMonthValidationAsync);' in text['endpoint'])
add('csv endpoint mapped', 'group.MapGet("/real-month-validation.csv", DownloadRealMonthValidationCsvAsync);' in text['endpoint'])
add('validation builder present', 'BuildRealMonthValidationAsync' in text['endpoint'] and 'BuildRealMonthValidationCsv' in text['endpoint'])
add('dto present', 'PayrollRealMonthValidationDto' in text['dtos'] and 'PayrollRealMonthEmployeeDto' in text['dtos'])
add('frontend card present', 'Attendance/Payroll real-month validation' in text['page'])
add('frontend csv export present', 'exportRealMonthValidationCsv' in text['page'])
add('backup manifest stage updated', 'Stage 11D-126 Attendance Payroll Real-Month Validation' in text['backup'])
add('docs present', 'Attendance/Payroll Real-Month Validation Closure - v4.12.41' in text['doc'])

failed = [name for name, ok in checks if not ok]
if failed:
    print('Failed checks:', file=sys.stderr)
    for name in failed:
        print(f'- {name}', file=sys.stderr)
    sys.exit(1)

print('Stage 11D-126 Attendance/Payroll real-month validation static checks passed.')

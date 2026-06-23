import sys
from pathlib import Path

root = Path(__file__).resolve().parents[2]
failed = []

def read(path: str) -> str:
    return (root / path).read_text(encoding='utf-8')

def add(name: str, ok: bool):
    if ok:
        print(f"OK: {name}")
    else:
        failed.append(name)
        print(f"FAIL: {name}", file=sys.stderr)

monthly = read('backend/Garmetix.Api/Hr/MonthlyAttendanceService.cs')
app_info = read('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs')
app_version = read('frontend/garmetix-web/utils/appVersion.ts')
csproj = read('backend/Garmetix.Api/Garmetix.Api.csproj')

add('build hotfix version identity', all(token in app_info for token in ['Version = "4.10.3"', 'Stage 9C Face Photo Review Build Hotfix', 'GARMETIX-9C-20260619-4103']) and "APP_VERSION = '4.10.3'" in app_version and '<Version>4.10.3</Version>' in csproj)
add('monthly attendance aliases legacy HRM entity', 'using HrAttendance = Garmetix.Core.Models.HRM.Attendance;' in monthly)
add('monthly attendance no ambiguous Attendance generic', 'IReadOnlyList<Attendance>' not in monthly and 'IReadOnlyList<HrAttendance>' in monthly)
add('monthly attendance still queries legacy daily attendance table', 'var attendanceRows = await db.Attendance' in monthly and 'CalculateSummary(employeeAttendance)' in monthly)

if failed:
    print('\nStage 9C build hotfix checks failed: ' + ', '.join(failed), file=sys.stderr)
    raise SystemExit(1)

print('\nStage 9C build hotfix static checks passed.')

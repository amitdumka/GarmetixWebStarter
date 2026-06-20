from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = []

def read(path: str) -> str:
    return (root / path).read_text(encoding='utf-8')

def add(name: str, ok: bool):
    checks.append((name, ok))

app_info = read('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs')
app_version = read('frontend/garmetix-web/utils/appVersion.ts')
csproj = read('backend/Garmetix.Api/Garmetix.Api.csproj')
dtos = read('backend/Garmetix.Api/Dashboard/DashboardDtos.cs')
endpoints = read('backend/Garmetix.Api/Dashboard/DashboardEndpoints.cs')
page = read('frontend/garmetix-web/pages/dashboard/todays/index.vue')
shell = read('frontend/garmetix-web/components/AppShell.vue')
access = read('frontend/garmetix-web/composables/useAccessControl.ts')
manifest = read('backend/Garmetix.Api/Testing/TestAutomationCatalog.cs')

add('version identity', (('Version = "4.10.10"' in app_info and 'GARMETIX-9K-20260620-4110' in app_info and "APP_VERSION = '4.10.10'" in app_version and '<Version>4.10.10</Version>' in csproj) or ('Version = "4.10.11"' in app_info and 'GARMETIX-10A-20260620-4111' in app_info and "APP_VERSION = '4.10.11'" in app_version and '<Version>4.10.11</Version>' in csproj)))
add('dashboard todays endpoint mapped', 'group.MapGet("/todays", TodaysAsync)' in endpoints and 'private static async Task<TodayDashboardDto> TodaysAsync' in endpoints)
add('dashboard todays dto exists', all(token in dtos for token in ['TodayDashboardDto', 'TodayCashFlowDto', 'TodayAttendanceSummaryDto', 'TodayEmployeeAttendanceDto']))
add('financial metrics included', all(token in endpoints for token in ['Today\'s Sales', 'Today\'s Purchase', 'Receipts', 'Payments', 'Expenses', 'Cash Vouchers']))
add('attendance active employee logic included', all(token in endpoints for token in ['AttendancePunches', 'EmployeeStatus == "Active"', 'Present Employees', 'Absent Employees']))
add('frontend page exists', all(token in page for token in ["Today's", 'Sales trend', 'Cash flow today', 'Employee present', 'Employee absent', 'salesPolyline', 'purchasePolyline']))
add('dashboard menu entry exists', "to: '/dashboard/todays'" in shell and "Today's" in shell)
add('route access rule exists', "path: '/dashboard/todays'" in access and "Today's" in access)
add('test automation manifest entry exists', 'TODAYS_DASHBOARD_ACCEPTANCE' in manifest and 'todays-dashboard-drill.sh' in manifest)

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(('PASS' if ok else 'FAIL') + f': {name}')
if failed:
    raise SystemExit('Stage 9K Today\'s Dashboard validation failed: ' + ', '.join(failed))
print('Stage 9K Today\'s Dashboard validation passed.')

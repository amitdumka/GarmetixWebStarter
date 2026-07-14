import sys
from pathlib import Path

root = Path(__file__).resolve().parents[2]
failed = []

def read(path: str) -> str:
    return (root / path).read_text(encoding='utf-8')

def exists(path: str) -> bool:
    return (root / path).exists()

def add(name: str, ok: bool):
    if ok:
        print(f"OK: {name}")
    else:
        failed.append(name)
        print(f"FAIL: {name}", file=sys.stderr)

app_info = read('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs')
app_version = read('frontend/garmetix-web/utils/appVersion.ts')
csproj = read('backend/Garmetix.Api/Garmetix.Api.csproj')
domain = read('backend/Garmetix.Domain/Attendance/AttendanceCore.cs')
db = read('backend/Garmetix.Infrastructure/Data/GarmetixDbContext.cs')
dtos = read('backend/Garmetix.Api/Attendance/Dtos/AttendanceDtos.cs')
endpoints = read('backend/Garmetix.Api/Attendance/AttendanceEndpoints.cs')
repair = read('backend/Garmetix.Api/Database/DatabaseSchemaRepairService.cs')
shell = read('frontend/garmetix-web/components/AppShell.vue')
access = read('frontend/garmetix-web/composables/useAccessControl.ts')
page = read('frontend/garmetix-web/pages/attendance/payroll-review.vue')
automation = read('backend/Garmetix.Api/Testing/TestAutomationCatalog.cs')

add('stage9d version identity', all(token in app_info for token in ['Version = "4.10.4"', 'Stage 9D Attendance Payroll Integration Foundation', 'GARMETIX-9D-20260619-4104']) and "APP_VERSION = '4.10.4'" in app_version and '<Version>4.10.4</Version>' in csproj)
add('attendance payroll review domain model', 'class AttendancePayrollReview' in domain and 'PayableDays' in domain and 'PayrollActionStatus' in domain)
add('attendance payroll review dbset/config', 'DbSet<AttendancePayrollReview>' in db and 'AttendancePayrollReviews' in db and 'EstimatedGrossPay' in db)
add('attendance payroll review migration', exists('backend/Garmetix.Infrastructure/Data/Migrations/20260619123000_AddAttendancePayrollReviewStage9D.cs'))
add('attendance payroll review schema repair', 'CREATE TABLE IF NOT EXISTS "AttendancePayrollReviews"' in repair and 'IX_AttendancePayrollReviews_CompanyId_StoreId_Year_Month_ReviewStatus' in repair)
add('attendance payroll review dto/api', 'AttendancePayrollReviewDto' in dtos and 'MapGet("/payroll-review"' in endpoints and 'RebuildPayrollReviewAsync' in endpoints and 'MarkPayrollReviewAsync' in endpoints)
add('no payroll auto posting', 'PayrollActionStatus = "NotPosted"' in endpoints and 'No salary auto-deduction' not in endpoints)
add('frontend payroll review page', exists('frontend/garmetix-web/pages/attendance/payroll-review.vue') and 'No salary auto-deduction' in page and 'Rebuild Review' in page)
add('nav and route access', '/attendance/payroll-review' in shell and "path: '/attendance/payroll-review'" in access)
add('automation and drill', 'ATTENDANCE_PAYROLL_REVIEW' in automation and exists('scripts/linux/attendance-payroll-review-drill.sh'))

if failed:
    print('\nStage 9D Attendance Payroll Review checks failed: ' + ', '.join(failed), file=sys.stderr)
    raise SystemExit(1)

print('\nStage 9D Attendance Payroll Review static checks passed.')

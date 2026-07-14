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
page = read('frontend/garmetix-web/pages/attendance/salary-draft.vue') if exists('frontend/garmetix-web/pages/attendance/salary-draft.vue') else ''
reports = read('frontend/garmetix-web/composables/useAttendanceReports.ts')
automation = read('backend/Garmetix.Api/Testing/TestAutomationCatalog.cs')

add('stage9e version identity', all(token in app_info for token in ['Version = "4.10.5"', 'Stage 9E Attendance Salary Slip Draft Preview', 'GARMETIX-9E-20260619-4105']) and "APP_VERSION = '4.10.5'" in app_version and '<Version>4.10.5</Version>' in csproj)
add('salary draft domain model', 'class AttendanceSalarySlipDraft' in domain and 'NetPayPreview' in domain and 'PayrollPostStatus' in domain)
add('salary draft dbset/config', 'DbSet<AttendanceSalarySlipDraft>' in db and 'AttendanceSalarySlipDrafts' in db and 'NetPayPreview' in db)
add('salary draft migration', exists('backend/Garmetix.Infrastructure/Data/Migrations/20260619133000_AddAttendanceSalarySlipDraftStage9E.cs'))
add('salary draft schema repair', 'CREATE TABLE IF NOT EXISTS "AttendanceSalarySlipDrafts"' in repair and 'IX_AttendanceSalarySlipDrafts_CompanyId_StoreId_Year_Month_DraftStatus' in repair)
add('salary draft dto/api', 'AttendanceSalarySlipDraftDto' in dtos and 'MapGet("/salary-slip-drafts"' in endpoints and 'RebuildSalarySlipDraftsAsync' in endpoints and 'MarkSalarySlipDraftAsync' in endpoints)
add('reviewed rows only', 'ReviewStatus == "Reviewed" || item.ReviewStatus == "ApprovedForPayroll"' in endpoints)
add('preview only no posting', 'PayrollPostStatus = "PreviewOnly"' in endpoints and 'SalaryPayments.Add' not in endpoints and 'SalaryPaySlips.Add' not in endpoints and 'AccountingPostingService' not in endpoints)
add('frontend salary draft page', exists('frontend/garmetix-web/pages/attendance/salary-draft.vue') and 'Preview only' in page and 'Rebuild Drafts' in page)
add('frontend composable/nav/access', 'salarySlipDrafts' in reports and '/attendance/salary-draft' in shell and "path: '/attendance/salary-draft'" in access)
add('automation and drill', 'ATTENDANCE_SALARY_DRAFT_PREVIEW' in automation and exists('scripts/linux/attendance-salary-draft-drill.sh'))

if failed:
    print('\nStage 9E Attendance Salary Draft checks failed: ' + ', '.join(failed), file=sys.stderr)
    raise SystemExit(1)

print('\nStage 9E Attendance Salary Draft static checks passed.')

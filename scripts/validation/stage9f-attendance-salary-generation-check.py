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
page = read('frontend/garmetix-web/pages/attendance/salary-draft.vue') if exists('frontend/garmetix-web/pages/attendance/salary-draft.vue') else ''
reports = read('frontend/garmetix-web/composables/useAttendanceReports.ts')
automation = read('backend/Garmetix.Api/Testing/TestAutomationCatalog.cs')

add('stage9f version identity', all(token in app_info for token in ['Version = "4.10.6"', 'Stage 9F Confirmed Salary Slip Generation', 'GARMETIX-9F-20260619-4106']) and "APP_VERSION = '4.10.6'" in app_version and '<Version>4.10.6</Version>' in csproj)
add('salary draft generation columns', 'GeneratedSalaryPaySlipId' in domain and 'GeneratedAtUtc' in domain and 'GeneratedBy' in domain and 'GeneratedSalaryPaySlipId' in db)
add('generation migration', exists('backend/Garmetix.Infrastructure/Data/Migrations/20260619143000_AddAttendanceSalarySlipGenerationStage9F.cs'))
add('generation schema repair', 'GeneratedSalaryPaySlipId' in repair and 'IX_AttendanceSalarySlipDrafts_GeneratedSalaryPaySlipId' in repair)
add('generation dto/api', 'AttendanceSalarySlipGenerateRequest' in dtos and 'AttendanceSalarySlipGenerateResultDto' in dtos and 'generate-payslips' in endpoints and 'GenerateSalarySlipsFromDraftsAsync' in endpoints)
add('explicit confirmation required', 'Explicit confirmation is required' in endpoints and 'request.Confirm' in endpoints)
add('ready rows only', 'DraftStatus == "ReadyForPayroll"' in endpoints and 'PayrollPostStatus != "SalarySlipGenerated"' in endpoints)
add('creates salary slips only', 'db.SalaryPaySlips.Add' in endpoints and 'SalaryPayments.Add' not in endpoints and 'AccountingPostingService' not in endpoints and 'PostSalaryPaymentAsync' not in endpoints)
add('no payment/voucher posting text', 'Salary payment and accounting voucher' in page and 'generateSalarySlipsFromDrafts' in reports)
add('automation and drill', 'ATTENDANCE_SALARY_SLIP_GENERATION' in automation and exists('scripts/linux/attendance-salary-generation-drill.sh'))

if failed:
    print('\nStage 9F Attendance Salary Generation checks failed: ' + ', '.join(failed), file=sys.stderr)
    raise SystemExit(1)

print('\nStage 9F Attendance Salary Generation static checks passed.')

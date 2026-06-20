from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
checks = []

def add(name, ok):
    checks.append((name, bool(ok)))

app_info = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
app_version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
csproj = (root / 'backend/Garmetix.Api/Garmetix.Api.csproj').read_text()
endpoints = (root / 'backend/Garmetix.Api/Attendance/AttendanceEndpoints.cs').read_text()
dtos = (root / 'backend/Garmetix.Api/Attendance/Dtos/AttendanceDtos.cs').read_text()
domain = (root / 'backend/Garmetix.Domain/Attendance/AttendanceCore.cs').read_text()
repair = (root / 'backend/Garmetix.Api/Database/DatabaseSchemaRepairService.cs').read_text()
nav = (root / 'frontend/garmetix-web/components/AppShell.vue').read_text()
access = (root / 'frontend/garmetix-web/composables/useAccessControl.ts').read_text()

add('version identity', all(token in app_info for token in ['Version = "4.10.9"', 'Stage 9 Complete Attendance Release', 'GARMETIX-9J-20260619-4109']) and "APP_VERSION = '4.10.9'" in app_version and '<Version>4.10.9</Version>' in csproj)
add('salary payment endpoints', all(token in endpoints for token in ['/salary-payment-candidates', '/salary-payments/generate', 'GenerateSalaryPaymentsFromDraftsAsync', 'AccountingPostingService', 'NextSalaryPaymentAsync']))
add('payment tracking fields', all(token in domain for token in ['GeneratedSalaryPaymentId', 'SalaryPaidAtUtc', 'SalaryPaidBy', 'PaymentPostStatus']))
add('payment dtos', all(token in dtos for token in ['AttendanceSalaryPaymentGenerateRequest', 'AttendanceSalaryPaymentCandidateDto', 'AttendanceSalaryPaymentGenerateResultDto']))
add('schema repair payment fields', all(token in repair for token in ['GeneratedSalaryPaymentId', 'SalaryPaidAtUtc', 'PaymentPostStatus']))
add('device bridge final acceptance endpoints', all(token in endpoints for token in ['/device-bridge/status', '/final-acceptance', 'DeviceBridgeStatusAsync', 'FinalAcceptanceAsync']))
add('frontend pages exist', all((root / f'frontend/garmetix-web/pages/attendance/{name}.vue').exists() for name in ['salary-payment', 'device-bridge', 'final-acceptance']))
add('nav and route access', all(token in nav and token in access for token in ['/attendance/salary-payment', '/attendance/device-bridge', '/attendance/final-acceptance']))
add('migration exists', (root / 'backend/Garmetix.Infrastructure/Data/Migrations/20260619153000_CompleteAttendanceStage9SalaryPayment.cs').exists())

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(f"{'PASS' if ok else 'FAIL'} - {name}")
if failed:
    print('\nStage 9 complete check failed: ' + ', '.join(failed))
    sys.exit(1)
print('\nStage 9 complete attendance check passed.')

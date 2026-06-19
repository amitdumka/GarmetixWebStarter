import sys
from pathlib import Path

root = Path(__file__).resolve().parents[2]
failed = []

def read(path: str) -> str:
    return (root / path).read_text(encoding='utf-8')

def exists(path: str) -> bool:
    return (root / path).exists()

def add(name: str, ok: bool):
    if not ok:
        failed.append(name)
        print(f"FAIL: {name}", file=sys.stderr)
    else:
        print(f"OK: {name}")

models = read('backend/Garmetix.Domain/Attendance/AttendanceCore.cs')
db = read('backend/Garmetix.Infrastructure/Data/GarmetixDbContext.cs')
endpoints = read('backend/Garmetix.Api/Attendance/AttendanceEndpoints.cs')
services = read('backend/Garmetix.Api/Attendance/Services/AttendanceService.cs')
repair = read('backend/Garmetix.Api/Database/DatabaseSchemaRepairService.cs')
program = read('backend/Garmetix.Api/Program.cs')
policies = read('backend/Garmetix.Api/Auth/GarmetixPolicies.cs')
matrix = read('backend/Garmetix.Api/Auth/AccessPermissionMatrix.cs')
app_info = read('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs')
app_version = read('frontend/garmetix-web/utils/appVersion.ts')
shell = read('frontend/garmetix-web/components/AppShell.vue')
access = read('frontend/garmetix-web/composables/useAccessControl.ts')
catalog = read('backend/Garmetix.Api/Testing/TestAutomationCatalog.cs')

required_models = ['AttendanceDevice','AttendancePunch','AttendanceShift','AttendancePolicy','EmployeeBiometricEnrollment','AttendanceRegularizationRequest','AttendanceApproval','AttendanceMonthlySummary']
add('attendance domain models', all(f'class {name}' in models for name in required_models))
add('attendance dbsets', all(f'DbSet<{name}>' in db for name in required_models))
add('attendance db indexes', 'AttendancePunch>().HasIndex' in db and 'EmployeeBiometricEnrollment>().HasIndex' in db and 'HasPrecision(12, 8)' in db)
add('attendance services registered', 'AddScoped<IAttendanceService, AttendanceService>()' in program and 'AddScoped<IAttendanceSyncService, AttendanceSyncService>()' in program)
add('attendance endpoints mapped', 'app.MapAttendanceEndpoints();' in program and 'MapAttendanceEndpoints' in endpoints)
add('attendance permission policy', 'public const string Attendance = "Attendance"' in policies and '[GarmetixPolicies.Attendance]' in matrix)
add('core attendance APIs', all(token in endpoints for token in ['/today','/monthly','/employee/{employeeId:guid}/history','/manual-punch','/recalculate','/lock-month','/payroll-summary']))
add('device APIs', all(token in endpoints for token in ['/devices/register','/devices/heartbeat','/devices/{id:guid}/revoke']))
add('kiosk APIs', all(token in endpoints for token in ['/api/attendance/kiosk','/bootstrap','/lookup-employee','/punch','/sync-pending']))
add('duplicate punch prevention', 'DuplicateWindowMinutesAsync' in services and 'Duplicate punch blocked' in services)
add('device token validation', 'HashToken' in services and 'ValidateDeviceAsync' in services and 'DeviceTokenHash' in models)
add('biometric placeholder only', 'FaceTemplateRef' in models and 'FingerprintTemplateRef' in models and 'Do not store raw' not in models)
add('schema repair attendance tables', 'RepairAttendanceCoreStorageAsync' in repair and all(table in repair for table in ['"AttendanceDevices"','"AttendancePunches"','"AttendanceShifts"','"AttendancePolicies"','"EmployeeBiometricEnrollments"','"AttendanceMonthlySummaries"']))
add('schema repair called at startup/manual repair', 'await RepairAttendanceCoreStorageAsync(db, logger, cancellationToken);' in repair)
add('migration present', exists('backend/Garmetix.Infrastructure/Data/Migrations/20260619093000_AddAttendanceCoreStage9A.cs'))
add('frontend attendance pages', all(exists(path) for path in [
    'frontend/garmetix-web/pages/attendance/index.vue',
    'frontend/garmetix-web/pages/attendance/today.vue',
    'frontend/garmetix-web/pages/attendance/monthly.vue',
    'frontend/garmetix-web/pages/attendance/devices.vue',
    'frontend/garmetix-web/pages/attendance/shifts.vue',
    'frontend/garmetix-web/pages/attendance/policies.vue',
    'frontend/garmetix-web/pages/attendance/regularization.vue',
    'frontend/garmetix-web/pages/attendance/biometric-enrollment.vue',
    'frontend/garmetix-web/pages/attendance/payroll-summary.vue'
]))
add('frontend composables/components', all(exists(path) for path in [
    'frontend/garmetix-web/composables/useAttendance.ts',
    'frontend/garmetix-web/composables/useAttendanceDevices.ts',
    'frontend/garmetix-web/composables/useAttendanceShifts.ts',
    'frontend/garmetix-web/composables/useAttendanceReports.ts',
    'frontend/garmetix-web/components/attendance/AttendanceTodayTable.vue',
    'frontend/garmetix-web/components/attendance/AttendanceMonthlyGrid.vue',
    'frontend/garmetix-web/components/attendance/AttendancePunchDrawer.vue',
    'frontend/garmetix-web/components/attendance/AttendanceDeviceCard.vue',
    'frontend/garmetix-web/components/attendance/AttendancePolicyForm.vue',
    'frontend/garmetix-web/components/attendance/AttendanceRegularizationList.vue'
]))
add('navigation and route access', 'Attendance Dashboard' in shell and '/attendance/payroll-summary' in shell and "path: '/attendance'" in access and "path: '/attendance/biometric-enrollment'" in access)
add('version identity', 'Version = "4.10.0"' in app_info and 'Stage 9A Attendance Core' in app_info and 'GARMETIX-9A-20260618-4100' in app_info and "APP_VERSION = '4.10.0'" in app_version)
add('test automation manifest', 'ATTENDANCE_CORE_ACCEPTANCE' in catalog)

if failed:
    print('\nStage 9A Attendance Core checks failed: ' + ', '.join(failed), file=sys.stderr)
    raise SystemExit(1)
print('\nStage 9A Attendance Core static checks passed.')

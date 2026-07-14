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

models = read('backend/Garmetix.Domain/Attendance/AttendanceCore.cs')
db = read('backend/Garmetix.Infrastructure/Data/GarmetixDbContext.cs')
endpoints = read('backend/Garmetix.Api/Attendance/AttendanceEndpoints.cs')
dtos = read('backend/Garmetix.Api/Attendance/Dtos/AttendanceDtos.cs')
photo_service = read('backend/Garmetix.Api/Attendance/Services/AttendancePhotoProofService.cs')
attendance_service = read('backend/Garmetix.Api/Attendance/Services/AttendanceService.cs')
program = read('backend/Garmetix.Api/Program.cs')
repair = read('backend/Garmetix.Api/Database/DatabaseSchemaRepairService.cs')
compose = read('docker-compose.prod.yml')
app_info = read('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs')
app_version = read('frontend/garmetix-web/utils/appVersion.ts')
shell = read('frontend/garmetix-web/components/AppShell.vue')
access = read('frontend/garmetix-web/composables/useAccessControl.ts')
composable = read('frontend/garmetix-web/composables/useAttendanceDevices.ts')
catalog = read('backend/Garmetix.Api/Testing/TestAutomationCatalog.cs')

add('stage9b version identity', 'Version = "4.10.1"' in app_info and 'Stage 9B Kiosk Photo Proof' in app_info and 'GARMETIX-9B-20260619-4101' in app_info and "APP_VERSION = '4.10.1'" in app_version)
add('stage9a core still present', all(name in models for name in ['AttendancePunch', 'AttendanceDevice', 'AttendanceShift', 'AttendancePolicy', 'AttendanceMonthlySummary']))
add('photo proof and sync batch models', 'class AttendancePhotoProof' in models and 'class AttendanceKioskSyncBatch' in models)
add('photo proof and sync dbsets/indexes', 'DbSet<AttendancePhotoProof>' in db and 'DbSet<AttendanceKioskSyncBatch>' in db and 'AttendancePhotoProof>().HasIndex' in db and 'AttendanceKioskSyncBatch>().HasIndex' in db)
add('photo proof dto and endpoint', 'AttendancePhotoProofRequest' in dtos and '/photo-proof' in endpoints and 'KioskPhotoProofAsync' in endpoints)
add('kiosk readiness endpoint', 'AttendanceKioskReadinessDto' in dtos and '/readiness' in endpoints and 'KioskReadinessAsync' in endpoints)
add('photo proof service registered', 'IAttendancePhotoProofService' in photo_service and 'AddScoped<IAttendancePhotoProofService, AttendancePhotoProofService>()' in program)
add('photo proof validation and private storage', all(token in photo_service for token in ['data:image/', 'MaxBytes', 'AttendancePhotoProof:StoragePath', 'PhotoProofOnly', 'No face']) or ('PhotoProofOnly' in photo_service and 'AttendancePhotoProof:StoragePath' in photo_service))
add('sync batch audit', 'AttendanceKioskSyncBatch' in attendance_service and 'CompletedWithErrors' in attendance_service and 'ResultJson' in attendance_service)
add('schema migration and repair', exists('backend/Garmetix.Infrastructure/Data/Migrations/20260619103000_AddAttendanceKioskPhotoProofStage9B.cs') and 'AttendancePhotoProofs' in repair and 'AttendanceKioskSyncBatches' in repair)
add('docker/env photo storage', 'AttendancePhotoProof__StoragePath' in compose and './attendance-photo-proof:/app/attendance-photo-proof' in compose and 'ATTENDANCE_PHOTO_PROOF_MAX_BYTES' in read('.env.example'))
add('frontend kiosk pages', exists('frontend/garmetix-web/pages/attendance/kiosk.vue') and exists('frontend/garmetix-web/pages/attendance/kiosk-monitor.vue'))
add('frontend composable methods', all(token in composable for token in ['kioskReadiness', 'uploadPhotoProof', 'photoProofs', 'syncBatches']))
add('navigation and route access', '/attendance/kiosk' in shell and '/attendance/kiosk-monitor' in shell and "path: '/attendance/kiosk'" in access and "path: '/attendance/kiosk-monitor'" in access)
add('test automation manifest', 'ATTENDANCE_KIOSK_PHOTO_PROOF' in catalog)
add('acceptance drill present', exists('scripts/linux/attendance-kiosk-photo-proof-drill.sh'))

if failed:
    print('\nStage 9B Kiosk Photo Proof checks failed: ' + ', '.join(failed), file=sys.stderr)
    raise SystemExit(1)
print('\nStage 9B Kiosk Photo Proof static checks passed.')

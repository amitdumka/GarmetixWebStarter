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
repair = read('backend/Garmetix.Api/Database/DatabaseSchemaRepairService.cs')
app_info = read('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs')
app_version = read('frontend/garmetix-web/utils/appVersion.ts')
shell = read('frontend/garmetix-web/components/AppShell.vue')
access = read('frontend/garmetix-web/composables/useAccessControl.ts')
composable = read('frontend/garmetix-web/composables/useAttendanceDevices.ts')
catalog = read('backend/Garmetix.Api/Testing/TestAutomationCatalog.cs')

add('stage9c version identity', 'Version = "4.10.3"' in app_info and 'Stage 9C Face Photo Review' in app_info and 'GARMETIX-9C-20260619-4103' in app_info and "APP_VERSION = '4.10.3'" in app_version)
add('photo proof review model fields', all(token in models for token in ['ReviewStatus', 'ReviewedAtUtc', 'ReviewedBy', 'ReviewRemarks', 'RegularizationRequestId']))
add('photo proof review db index', 'ReviewStatus' in db and 'CapturedAtUtc' in db and 'AttendancePhotoProof>().HasIndex' in db)
add('review dto and summary dto', 'AttendancePhotoProofReviewRequest' in dtos and 'AttendancePhotoProofReviewSummaryDto' in dtos)
add('review endpoints', all(token in endpoints for token in ['/photo-proofs/review-summary', 'ReviewPhotoProofAsync', 'CreateRegularizationFromPhotoProofAsync', 'NormalizePhotoReviewDecision']))
add('regularization linkage', 'PhotoProofReview' in endpoints and 'RegularizationRequestId' in endpoints and 'AttendanceRegularizationRequest' in endpoints)
add('schema migration and repair', exists('backend/Garmetix.Infrastructure/Data/Migrations/20260619113000_AddAttendanceFacePhotoReviewStage9C.cs') and 'ADD COLUMN IF NOT EXISTS "ReviewStatus"' in repair and 'RegularizationRequestId' in repair)
add('frontend photo review page', exists('frontend/garmetix-web/pages/attendance/photo-review.vue') and 'Face Photo Review' in read('frontend/garmetix-web/pages/attendance/photo-review.vue') and 'No face matching' in read('frontend/garmetix-web/pages/attendance/photo-review.vue'))
add('frontend composable review methods', all(token in composable for token in ['photoProofReviewSummary', 'reviewPhotoProof', 'createPhotoProofRegularization']))
add('navigation and route access', '/attendance/photo-review' in shell and "path: '/attendance/photo-review'" in access)
add('test automation manifest', 'ATTENDANCE_FACE_PHOTO_REVIEW' in catalog)
add('acceptance drill present', exists('scripts/linux/attendance-face-photo-review-drill.sh'))

if failed:
    print('\nStage 9C Face Photo Review checks failed: ' + ', '.join(failed), file=sys.stderr)
    raise SystemExit(1)
print('\nStage 9C Face Photo Review static checks passed.')

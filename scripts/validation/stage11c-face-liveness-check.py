from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
checks: list[tuple[str, bool]] = []


def read(path: str) -> str:
    return (root / path).read_text(encoding="utf-8")


def exists(path: str) -> bool:
    return (root / path).exists()


def add(name: str, ok: bool):
    checks.append((name, ok))


app_info = read("backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs")
app_version = read("frontend/garmetix-web/utils/appVersion.ts")
api_project = read("backend/Garmetix.Api/Garmetix.Api.csproj")
kiosk_project = read("apps/Garmetix.AttendanceKiosk/Garmetix.AttendanceKiosk.csproj")
bridge_project = read("apps/Garmetix.FingerprintBridge/Garmetix.FingerprintBridge.csproj")
mock_project = read("apps/Garmetix.MantraMockService/Garmetix.MantraMockService.csproj")
attendance_endpoints = read("backend/Garmetix.Api/Attendance/AttendanceEndpoints.cs")
attendance_composable = read("frontend/garmetix-web/composables/useAttendance.ts")
attendance_reports = read("frontend/garmetix-web/composables/useAttendanceReports.ts")
page = read("frontend/garmetix-web/pages/attendance/face-liveness.vue")
access = read("frontend/garmetix-web/composables/useAccessControl.ts")
app_shell = read("frontend/garmetix-web/components/AppShell.vue")
legacy_shell = read("frontend/garmetix-web/components/AppShellLegacy.vue")
readme = read("README.md")
roadmap = read("docs/planning/CURRENT-ROADMAP.md")
operations_doc = read("docs/operations/Stage11C-Face-Liveness-Readiness-Contract-v4.11.13.md")
current_release = read("scripts/validation/current-release-checks.py")

add(
    "version identity",
    all(token in app_info for token in ['Version = "4.11.15"', "Stage 11D Migration Baseline And System Defaults", "GARMETIX-11D-20260622-4115"])
    and "APP_VERSION = '4.11.15'" in app_version
    and "Stage 11D Migration Baseline And System Defaults" in app_version
    and "GARMETIX-11D-20260622-4115" in app_version
    and "<Version>4.11.15</Version>" in api_project
    and "<ApplicationDisplayVersion>4.11.15</ApplicationDisplayVersion>" in kiosk_project
    and "<ApplicationVersion>4115</ApplicationVersion>" in kiosk_project
    and "<Version>4.11.15</Version>" in bridge_project
    and "<Version>4.11.15</Version>" in mock_project,
)
add(
    "face liveness endpoint contract",
    'group.MapGet("/face-liveness/status", FaceLivenessStatusAsync)' in attendance_endpoints
    and "FaceLivenessStatusAsync" in attendance_endpoints
    and "faceLivenessEnabled = false" in attendance_endpoints
    and "realFaceRecognitionEnabled = false" in attendance_endpoints
    and "rawFaceTemplateStorageAllowed = false" in attendance_endpoints
    and "photoProofEvidenceEnabled = true" in attendance_endpoints
    and "Message Logs" in attendance_endpoints
    and "consentRequired = true" in attendance_endpoints,
)
add(
    "raw face payload fields blocked",
    all(token in attendance_endpoints for token in ["blockedResponseFields", "rawFaceImage", "faceEmbedding", "faceTemplateBase64", "landmarks", "biometricPayload", "templateData"])
    and "rawPayloadStored=false" in attendance_endpoints
    and "reference-only storage" in attendance_endpoints,
)
add(
    "frontend API wiring",
    "faceLivenessStatus" in attendance_composable
    and "attendance/face-liveness/status" in attendance_composable
    and "faceLivenessStatus: attendance.faceLivenessStatus" in attendance_reports,
)
add(
    "face liveness page",
    exists("frontend/garmetix-web/pages/attendance/face-liveness.vue")
    and "Face Liveness Readiness" in page
    and "reports.faceLivenessStatus" in page
    and "blockedResponseFields" in page
    and "providerCandidates" in page
    and "readinessChecklist" in page
    and "nextAfterThisPart" in page
    and "/attendance/photo-review" in page
    and "/attendance/biometric-enrollment" in page,
)
add(
    "navigation and role access",
    "to: '/attendance/face-liveness'" in app_shell
    and "to: '/attendance/face-liveness'" in legacy_shell
    and "i-lucide-scan-face" in app_shell
    and "i-lucide-scan-face" in legacy_shell
    and "path: '/attendance/face-liveness'" in access
    and "Face Liveness Readiness" in access,
)
add(
    "documentation and roadmap",
    "v4.11.13 Stage 11C Face Liveness Readiness Contract" in readme
    and exists("docs/operations/Stage11C-Face-Liveness-Readiness-Contract-v4.11.13.md")
    and "Stage 11C Face Liveness Readiness Contract" in operations_doc
    and "rawFaceImage" in operations_doc
    and "Message Logs" in operations_doc
    and "Current version: 4.11.15" in roadmap
    and "Stage 11C Face Liveness Readiness Contract" in roadmap,
)
add(
    "current release chain includes stage11c",
    "stage11c-face-liveness-check.py" in current_release,
)

failed = [name for name, ok in checks if not ok]
if failed:
    print("Stage 11C Face Liveness checks failed: " + ", ".join(failed), file=sys.stderr)
    sys.exit(1)

print("Stage 11C Face Liveness Readiness Contract validation passed.")

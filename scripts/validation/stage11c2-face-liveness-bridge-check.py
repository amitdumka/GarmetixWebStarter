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
attendance_dtos = read("backend/Garmetix.Api/Attendance/Dtos/AttendanceDtos.cs")
attendance_composable = read("frontend/garmetix-web/composables/useAttendance.ts")
attendance_reports = read("frontend/garmetix-web/composables/useAttendanceReports.ts")
page = read("frontend/garmetix-web/pages/attendance/face-liveness.vue")
readme = read("README.md")
roadmap = read("docs/planning/CURRENT-ROADMAP.md")
operations_doc = read("docs/operations/Stage11C2-Face-Liveness-Simulator-Bridge-v4.11.14.md")
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
    "face bridge dtos",
    "FaceLivenessBridgeRequest" in attendance_dtos
    and "FaceLivenessBridgeResultDto" in attendance_dtos
    and "LivenessScore" in attendance_dtos
    and "ConsentAuditRef" in attendance_dtos
    and "RawPayloadStored" in attendance_dtos,
)
add(
    "simulator endpoint contract",
    'group.MapGet("/face-liveness/simulator/health", FaceLivenessSimulatorHealthAsync)' in attendance_endpoints
    and 'group.MapPost("/face-liveness/simulator/proof", FaceLivenessSimulatorProofAsync)' in attendance_endpoints
    and 'group.MapPost("/face-liveness/simulator/verify", FaceLivenessSimulatorVerifyAsync)' in attendance_endpoints
    and "RunFaceLivenessSimulatorAsync" in attendance_endpoints
    and "RawPayloadBlocked" in attendance_endpoints
    and "Scenario=RawPayload" in attendance_endpoints
    and "Attendance Face Liveness" in attendance_endpoints,
)
add(
    "external bridge contract",
    'group.MapPost("/face-liveness/external/health", FaceLivenessExternalHealthAsync)' in attendance_endpoints
    and 'group.MapPost("/face-liveness/external/proof", FaceLivenessExternalProofAsync)' in attendance_endpoints
    and 'group.MapPost("/face-liveness/external/verify", FaceLivenessExternalVerifyAsync)' in attendance_endpoints
    and "RunFaceLivenessExternalAsync" in attendance_endpoints
    and "BuildFaceLivenessExternalPayload" in attendance_endpoints
    and "rawPayloadAllowed = false" in attendance_endpoints
    and "local/private" in attendance_endpoints,
)
add(
    "raw face fields blocked",
    all(token in attendance_endpoints for token in ["rawFaceImage", "faceImage", "faceEmbedding", "faceTemplateBase64", "landmarks", "templateData", "biometricPayload"])
    and "DetectRawBiometricFields" in attendance_endpoints
    and "RawPayloadStored" in attendance_endpoints,
)
add(
    "frontend simulator and external controls",
    all(token in attendance_composable for token in [
        "faceLivenessSimulatorHealth",
        "attendance/face-liveness/simulator/proof",
        "attendance/face-liveness/simulator/verify",
        "attendance/face-liveness/external/health",
        "attendance/face-liveness/external/proof",
        "attendance/face-liveness/external/verify",
    ])
    and "faceLivenessSimulatorProof: attendance.faceLivenessSimulatorProof" in attendance_reports
    and "faceLivenessExternalVerify: attendance.faceLivenessExternalVerify" in attendance_reports
    and "runSimulator" in page
    and "runExternal" in page
    and "Block Raw" in page
    and "externalBridgeUrl" in page
    and "rawPayloadStored" in page,
)
add(
    "docs and release chain",
    exists("docs/operations/Stage11C2-Face-Liveness-Simulator-Bridge-v4.11.14.md")
    and "Stage 11C-2 Face Liveness Simulator Bridge" in operations_doc
    and "Simulator `RawPayload` blocking scenario" in operations_doc
    and "Attendance Face Liveness" in operations_doc
    and "v4.11.14 Stage 11C-2 Face Liveness Simulator Bridge" in readme
    and "Current version: 4.11.15" in roadmap
    and "Stage 11C-3 local face/liveness bridge template" in roadmap
    and "stage11c2-face-liveness-bridge-check.py" in current_release,
)

failed = [name for name, ok in checks if not ok]
if failed:
    print("Stage 11C-2 Face Liveness Bridge checks failed: " + ", ".join(failed), file=sys.stderr)
    sys.exit(1)

print("Stage 11C-2 Face Liveness Simulator Bridge validation passed.")

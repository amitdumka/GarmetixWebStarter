from pathlib import Path

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
attendance_endpoints = read("backend/Garmetix.Api/Attendance/AttendanceEndpoints.cs")
device_bridge_page = read("frontend/garmetix-web/pages/attendance/device-bridge.vue")
access = read("frontend/garmetix-web/composables/useAccessControl.ts")
app_shell = read("frontend/garmetix-web/components/AppShell.vue")
legacy_shell = read("frontend/garmetix-web/components/AppShellLegacy.vue")
readme = read("README.md")
roadmap = read("docs/planning/CURRENT-ROADMAP.md")
operations_doc = read("docs/operations/Stage11B3-External-Fingerprint-Bridge-Connector-v4.11.5.md")

add(
    "version identity",
    all(token in app_info for token in ['Version = "4.11.5"', "Stage 11B-3 External Fingerprint Bridge Connector", "GARMETIX-11B-20260621-4115"])
    and "APP_VERSION = '4.11.5'" in app_version
    and "Stage 11B-3 External Fingerprint Bridge Connector" in app_version
    and "GARMETIX-11B-20260621-4115" in app_version
    and "<Version>4.11.5</Version>" in api_project
    and "<ApplicationDisplayVersion>4.11.5</ApplicationDisplayVersion>" in kiosk_project
    and "<ApplicationVersion>4115</ApplicationVersion>" in kiosk_project,
)
add(
    "device bridge endpoint contract",
    'group.MapGet("/device-bridge/status", DeviceBridgeStatusAsync)' in attendance_endpoints
    and "ContractReady" in attendance_endpoints
    and "fingerprintBridgeEnabled = false" in attendance_endpoints
    and "rawFingerprintStorageAllowed = false" in attendance_endpoints
    and "adapterCandidates" in attendance_endpoints
    and "Simulator adapter" in attendance_endpoints
    and "localBridgeBaseUrl" in attendance_endpoints
    and "POST /identify" in attendance_endpoints
    and "POST /enroll" in attendance_endpoints
    and "Do not store raw fingerprint image" in attendance_endpoints
    and "Message Logs" in attendance_endpoints,
)
add(
    "simulator endpoint contract",
    'group.MapGet("/device-bridge/simulator/health", DeviceBridgeSimulatorHealthAsync)' in attendance_endpoints
    and 'group.MapPost("/device-bridge/simulator/capture", DeviceBridgeSimulatorCaptureAsync)' in attendance_endpoints
    and 'group.MapPost("/device-bridge/simulator/identify", DeviceBridgeSimulatorIdentifyAsync)' in attendance_endpoints
    and 'group.MapPost("/device-bridge/simulator/enroll", DeviceBridgeSimulatorEnrollAsync)' in attendance_endpoints
    and "FingerprintBridgeSimulatorRequest" in attendance_endpoints
    and "FingerprintBridgeSimulatorResultDto" in attendance_endpoints
    and "RawPayloadStored" in attendance_endpoints
    and "logs.SuccessAsync" in attendance_endpoints
    and "logs.ErrorAsync" in attendance_endpoints,
)
add(
    "external bridge connector contract",
    'group.MapPost("/device-bridge/external/health", DeviceBridgeExternalHealthAsync)' in attendance_endpoints
    and 'group.MapPost("/device-bridge/external/capture", DeviceBridgeExternalCaptureAsync)' in attendance_endpoints
    and 'group.MapPost("/device-bridge/external/identify", DeviceBridgeExternalIdentifyAsync)' in attendance_endpoints
    and 'group.MapPost("/device-bridge/external/enroll", DeviceBridgeExternalEnrollAsync)' in attendance_endpoints
    and "FingerprintBridgeExternalRequest" in attendance_endpoints
    and "TryBuildExternalBridgeUri" in attendance_endpoints
    and "IsAllowedBridgeHost" in attendance_endpoints
    and "DetectRawBiometricFields" in attendance_endpoints
    and "PostAsJsonAsync" in attendance_endpoints
    and "logs.SuccessAsync" in attendance_endpoints
    and "logs.ErrorAsync" in attendance_endpoints,
)
add(
    "fingerprint bridge page",
    "Fingerprint Bridge" in device_bridge_page
    and "Stage 11B defines the vendor-neutral fingerprint bridge contract" in device_bridge_page
    and "adapterCandidates" in device_bridge_page
    and "bridgeContract" in device_bridge_page
    and "Simulator handshake" in device_bridge_page
    and "deviceBridgeSimulatorHealth" in device_bridge_page
    and "deviceBridgeSimulatorCapture" in device_bridge_page
    and "deviceBridgeSimulatorIdentify" in device_bridge_page
    and "deviceBridgeSimulatorEnroll" in device_bridge_page
    and "rawPayloadStored" in device_bridge_page
    and "privacyRules" in device_bridge_page
    and "implementationChecklist" in device_bridge_page
    and "rehearsalSteps" in device_bridge_page
    and "nextAfterThisPart" in device_bridge_page,
)
add(
    "external bridge page",
    "External bridge connector" in device_bridge_page
    and "externalBridgeUrl" in device_bridge_page
    and "deviceBridgeExternalHealth" in device_bridge_page
    and "deviceBridgeExternalCapture" in device_bridge_page
    and "deviceBridgeExternalIdentify" in device_bridge_page
    and "deviceBridgeExternalEnroll" in device_bridge_page
    and "rawPayloadStored" in device_bridge_page,
)
add(
    "route access and navigation",
    "path: '/attendance/device-bridge'" in access
    and "Fingerprint Bridge" in access
    and "/attendance/device-bridge" in app_shell
    and "stage 11b" in app_shell
    and "/attendance/device-bridge" in legacy_shell
    and "stage 11b" in legacy_shell,
)
add(
    "docs and roadmap",
    exists("docs/operations/Stage11B3-External-Fingerprint-Bridge-Connector-v4.11.5.md")
    and "Stage 11B-3 External Fingerprint Bridge Connector" in readme
    and "Stage 11B-3 External Fingerprint Bridge Connector" in roadmap
    and "Select fingerprint hardware/vendor SDK" in roadmap
    and "Blocked Fields" in operations_doc
    and "rawPayloadStored = false" in operations_doc
    and "8s timeout" in operations_doc
    and "Message Logs" in operations_doc
    and "Select fingerprint hardware and SDK" in operations_doc,
)

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(("PASS" if ok else "FAIL") + f": {name}")
if failed:
    raise SystemExit("Stage 11B fingerprint bridge validation failed: " + ", ".join(failed))
print("Stage 11B-3 external fingerprint bridge connector validation passed.")

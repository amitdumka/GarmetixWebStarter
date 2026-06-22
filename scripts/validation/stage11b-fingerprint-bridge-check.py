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
bridge_project = read("apps/Garmetix.FingerprintBridge/Garmetix.FingerprintBridge.csproj")
bridge_program = read("apps/Garmetix.FingerprintBridge/Program.cs")
bridge_readme = read("apps/Garmetix.FingerprintBridge/README.md")
attendance_endpoints = read("backend/Garmetix.Api/Attendance/AttendanceEndpoints.cs")
attendance_service = read("backend/Garmetix.Api/Attendance/Services/AttendanceService.cs")
attendance_dtos = read("backend/Garmetix.Api/Attendance/Dtos/AttendanceDtos.cs")
device_bridge_page = read("frontend/garmetix-web/pages/attendance/device-bridge.vue")
kiosk_page = read("frontend/garmetix-web/pages/attendance/kiosk.vue")
env_example = read(".env.example")
compose_prod = read("docker-compose.prod.yml")
access = read("frontend/garmetix-web/composables/useAccessControl.ts")
app_shell = read("frontend/garmetix-web/components/AppShell.vue")
legacy_shell = read("frontend/garmetix-web/components/AppShellLegacy.vue")
readme = read("README.md")
roadmap = read("docs/planning/CURRENT-ROADMAP.md")
operations_doc = read("docs/operations/Stage11B5-Fingerprint-Kiosk-Punch-Guard-v4.11.7.md")

add(
    "version identity",
    all(token in app_info for token in ['Version = "4.11.7"', "Stage 11B-5 Fingerprint Kiosk Punch Guard", "GARMETIX-11B-20260621-4117"])
    and "APP_VERSION = '4.11.7'" in app_version
    and "Stage 11B-5 Fingerprint Kiosk Punch Guard" in app_version
    and "GARMETIX-11B-20260621-4117" in app_version
    and "<Version>4.11.7</Version>" in api_project
    and "<ApplicationDisplayVersion>4.11.7</ApplicationDisplayVersion>" in kiosk_project
    and "<ApplicationVersion>4117</ApplicationVersion>" in kiosk_project,
)
add(
    "local bridge template project",
    exists("apps/Garmetix.FingerprintBridge/Garmetix.FingerprintBridge.csproj")
    and '<TargetFramework>net10.0</TargetFramework>' in bridge_project
    and '<Version>4.11.7</Version>' in bridge_project
    and "4.11.7-stage11b5-fingerprint-kiosk-punch-guard" in bridge_project
    and "IFingerprintVendorAdapter" in bridge_program
    and "SimulatorFingerprintVendorAdapter" in bridge_program
    and "MapBridgeRoutes(app.MapGroup(\"/garmetix-fingerprint\"))" in bridge_program
    and 'routes.MapGet("/health"' in bridge_program
    and 'routes.MapPost("/capture"' in bridge_program
    and 'routes.MapPost("/identify"' in bridge_program
    and 'routes.MapPost("/enroll"' in bridge_program
    and "RawPayloadStored" in bridge_program
    and "rawPayloadAllowed was ignored" in bridge_program
    and "IsAllowedLocalCaller" in bridge_program,
)
add(
    "kiosk fingerprint punch guard backend",
    "AttendanceFingerprintOptions" in attendance_service
    and "ValidateFingerprintProof" in attendance_service
    and "FingerprintAcceptedStatuses" in attendance_service
    and "FingerprintMatched" in attendance_service
    and "Attendance Fingerprint Guard" in attendance_service
    and "KioskPunchBlocked" in attendance_service
    and "AttendanceFingerprintProofDto" in attendance_dtos
    and "FingerprintProof" in attendance_dtos
    and "FingerprintPunchRequired" in attendance_dtos
    and "FingerprintBridgeBaseUrl" in attendance_dtos
    and "fingerprint.IsRequiredForStore" in attendance_endpoints
    and "Fingerprint proof must come from local bridge identify response." in attendance_endpoints,
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
    "local bridge template page",
    "Local bridge template" in device_bridge_page
    and "localBridgeTemplate" in device_bridge_page
    and "projectPath" in device_bridge_page
    and "runCommand" in device_bridge_page
    and "defaultBaseUrl" in device_bridge_page
    and "adapterClass" in device_bridge_page,
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
    "web kiosk fingerprint guard",
    "Fingerprint Guard" in kiosk_page
    and "fingerprintBridgeUrl" in kiosk_page
    and "verifyFingerprint" in kiosk_page
    and "fingerprintRequired" in kiosk_page
    and "fingerprintProof" in kiosk_page
    and "fingerprintOfflineQueueAllowed" in kiosk_page
    and "fetch(bridgeEndpoint('identify')" in kiosk_page,
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
    exists("docs/operations/Stage11B5-Fingerprint-Kiosk-Punch-Guard-v4.11.7.md")
    and "Stage 11B-5 Fingerprint Kiosk Punch Guard" in readme
    and "Stage 11B-5 Fingerprint Kiosk Punch Guard" in roadmap
    and "Select fingerprint hardware/vendor SDK" in roadmap
    and "Stage 11B-5 Fingerprint Kiosk Punch Guard" in operations_doc
    and "ATTENDANCE_FINGERPRINT_KIOSK_PUNCH_MODE" in operations_doc
    and "Attendance Fingerprint Guard" in operations_doc
    and "rawPayloadStored = false" in operations_doc
    and "rawImage" in operations_doc
    and "Select the actual fingerprint hardware" in operations_doc
    and "ATTENDANCE_FINGERPRINT_KIOSK_PUNCH_MODE=Off" in env_example
    and "AttendanceFingerprint__KioskPunchMode" in compose_prod,
)

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(("PASS" if ok else "FAIL") + f": {name}")
if failed:
    raise SystemExit("Stage 11B fingerprint bridge validation failed: " + ", ".join(failed))
print("Stage 11B-5 Fingerprint Kiosk Punch Guard validation passed.")

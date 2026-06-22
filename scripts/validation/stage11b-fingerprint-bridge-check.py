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
mock_mantra_project = read("apps/Garmetix.MantraMockService/Garmetix.MantraMockService.csproj")
mock_mantra_program = read("apps/Garmetix.MantraMockService/Program.cs")
mock_mantra_readme = read("apps/Garmetix.MantraMockService/README.md")
attendance_endpoints = read("backend/Garmetix.Api/Attendance/AttendanceEndpoints.cs")
attendance_service = read("backend/Garmetix.Api/Attendance/Services/AttendanceService.cs")
attendance_dtos = read("backend/Garmetix.Api/Attendance/Dtos/AttendanceDtos.cs")
device_bridge_page = read("frontend/garmetix-web/pages/attendance/device-bridge.vue")
kiosk_page = read("frontend/garmetix-web/pages/attendance/kiosk.vue")
biometric_enrollment_page = read("frontend/garmetix-web/pages/attendance/biometric-enrollment.vue")
env_example = read(".env.example")
compose_prod = read("docker-compose.prod.yml")
access = read("frontend/garmetix-web/composables/useAccessControl.ts")
app_shell = read("frontend/garmetix-web/components/AppShell.vue")
legacy_shell = read("frontend/garmetix-web/components/AppShellLegacy.vue")
readme = read("README.md")
roadmap = read("docs/planning/CURRENT-ROADMAP.md")
operations_doc = read("docs/operations/Stage11B5-Fingerprint-Kiosk-Punch-Guard-v4.11.7.md")
operations_doc_11b6 = read("docs/operations/Stage11B6-Biometric-Enrollment-Consent-Hardening-v4.11.8.md")
operations_doc_11b7 = read("docs/operations/Stage11B7-Mantra-Enrollment-Bridge-Wiring-v4.11.9.md")
operations_doc_11b8 = read("docs/operations/Stage11B8-Mantra-Local-Service-Adapter-v4.11.10.md")
operations_doc_11b9 = read("docs/operations/Stage11B9-Mantra-Service-Harness-v4.11.11.md")

add(
    "version identity",
    all(token in app_info for token in ['Version = "4.11.11"', "Stage 11B-9 Mantra Service Harness", "GARMETIX-11B-20260622-4121"])
    and "APP_VERSION = '4.11.11'" in app_version
    and "Stage 11B-9 Mantra Service Harness" in app_version
    and "GARMETIX-11B-20260622-4121" in app_version
    and "<Version>4.11.11</Version>" in api_project
    and "<ApplicationDisplayVersion>4.11.11</ApplicationDisplayVersion>" in kiosk_project
    and "<ApplicationVersion>4121</ApplicationVersion>" in kiosk_project,
)
add(
    "local bridge template project",
    exists("apps/Garmetix.FingerprintBridge/Garmetix.FingerprintBridge.csproj")
    and '<TargetFramework>net10.0</TargetFramework>' in bridge_project
    and '<Version>4.11.11</Version>' in bridge_project
    and "4.11.11-stage11b9-mantra-service-harness" in bridge_project
    and "IFingerprintVendorAdapter" in bridge_program
    and "SimulatorFingerprintVendorAdapter" in bridge_program
    and "MantraFingerprintVendorAdapter" in bridge_program
    and "Bridge:Adapter=Mantra" in bridge_readme
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
    "mantra mock service harness",
    exists("apps/Garmetix.MantraMockService/Garmetix.MantraMockService.csproj")
    and '<TargetFramework>net10.0</TargetFramework>' in mock_mantra_project
    and '<Version>4.11.11</Version>' in mock_mantra_project
    and "4.11.11-stage11b9-mantra-service-harness" in mock_mantra_project
    and 'app.MapGet("/health"' in mock_mantra_program
    and 'app.MapPost("/capture"' in mock_mantra_program
    and 'app.MapPost("/identify"' in mock_mantra_program
    and 'app.MapPost("/enroll"' in mock_mantra_program
    and 'app.MapPost("/unsafe/enroll-with-raw"' in mock_mantra_program
    and "rawImage" in mock_mantra_program
    and "rawPayloadStored = false" in mock_mantra_program
    and "IsAllowedLocalCaller" in mock_mantra_program
    and "Bridge:MantraServiceUrl=http://127.0.0.1:8788/" in mock_mantra_readme,
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
    "mantra selected adapter boundary",
    "selectedFingerprintHardware = \"Mantra MFS100 / MIS100\"" in attendance_endpoints
    and "selectedBridgeAdapter = \"MantraFingerprintVendorAdapter\"" in attendance_endpoints
    and "decisionStatus = \"Selected\"" in attendance_endpoints
    and "Bridge:Adapter=Mantra" in attendance_endpoints
    and "SdkNotConfigured" in bridge_program
    and "Mantra adapter boundary is selected" in bridge_program
    and "MantraServiceUrl" in bridge_program
    and "ContainsRawBiometricField" in bridge_program
    and "RawPayloadBlocked" in bridge_program
    and "IsAllowedMantraServiceHost" in bridge_program
    and "Mantra enrollment bridge" in biometric_enrollment_page
    and "Mantra Bridge Enroll" in biometric_enrollment_page
    and "enrollFromBridge('external')" in biometric_enrollment_page
    and "deviceBridgeExternalEnroll" in biometric_enrollment_page
    and "deviceBridgeSimulatorEnroll" in biometric_enrollment_page
    and "lastBridgeResult" in biometric_enrollment_page,
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
    "mantra mock service page and status",
    "mantraMockService" in attendance_endpoints
    and "apps/Garmetix.MantraMockService/Garmetix.MantraMockService.csproj" in attendance_endpoints
    and "Bridge:MantraServiceUrl=http://127.0.0.1:8788/" in attendance_endpoints
    and "POST /unsafe/enroll-with-raw" in attendance_endpoints
    and "Mantra mock service" in device_bridge_page
    and "status?.mantraMockService?.projectPath" in device_bridge_page
    and "rawBlockingRoute" in device_bridge_page,
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
    "biometric enrollment consent hardening",
    "BiometricEnrollmentSaveRequest" in attendance_dtos
    and "BiometricEnrollmentRowDto" in attendance_dtos
    and "CreateBiometricEnrollmentAsync(BiometricEnrollmentSaveRequest" in attendance_endpoints
    and "BuildBiometricEnrollmentRow" in attendance_endpoints
    and "Attendance Biometric Enrollment" in attendance_endpoints
    and "EnrollmentRevoked" in attendance_endpoints
    and "SaveAsync(BiometricEnrollmentSaveRequest" in attendance_service
    and "BlockedTemplateTokens" in attendance_service
    and "Employee consent is required before saving biometric template references." in attendance_service
    and "RawBiometricPayloadStored = false" in attendance_service
    and "Biometric Enrollment" in biometric_enrollment_page
    and "Save Enrollment" in biometric_enrollment_page
    and "consentGiven" in biometric_enrollment_page
    and "fingerprintTemplateRef" in biometric_enrollment_page
    and "revoke(row)" in biometric_enrollment_page
    and "Raw biometric payloads are blocked" in biometric_enrollment_page,
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
    and exists("docs/operations/Stage11B6-Biometric-Enrollment-Consent-Hardening-v4.11.8.md")
    and exists("docs/operations/Stage11B7-Mantra-Enrollment-Bridge-Wiring-v4.11.9.md")
    and exists("docs/operations/Stage11B8-Mantra-Local-Service-Adapter-v4.11.10.md")
    and exists("docs/operations/Stage11B9-Mantra-Service-Harness-v4.11.11.md")
    and "Stage 11B-9 Mantra Service Harness" in readme
    and "Stage 11B-9 Mantra Service Harness" in roadmap
    and "Stage 11B-6 Biometric Enrollment Consent Hardening" in operations_doc_11b6
    and "BiometricEnrollmentSaveRequest" in operations_doc_11b6
    and "Attendance Biometric Enrollment" in operations_doc_11b6
    and "RawBiometricPayloadStored = false" in operations_doc_11b6
    and "rawImage" in operations_doc_11b6
    and "Stage 11B-7 Mantra Enrollment Bridge Wiring" in operations_doc_11b7
    and "MantraFingerprintVendorAdapter" in operations_doc_11b7
    and "Bridge:Adapter=Mantra" in operations_doc_11b7
    and "rawPayloadStored = false" in operations_doc_11b7
    and "Stage 11B-8 Mantra Local Service Adapter" in operations_doc_11b8
    and "Bridge:MantraServiceUrl" in operations_doc_11b8
    and "templateData" in operations_doc_11b8
    and "Stage 11B-9 Mantra Service Harness" in operations_doc_11b9
    and "apps/Garmetix.MantraMockService" in operations_doc_11b9
    and "RawPayloadBlocked" in operations_doc_11b9
    and "Stage 11B-5 Fingerprint Kiosk Punch Guard" in readme
    and "Stage 11B-5 Fingerprint Kiosk Punch Guard" in roadmap
    and "Install the official Mantra SDK/service" in roadmap
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
print("Stage 11B-9 Mantra Service Harness validation passed.")

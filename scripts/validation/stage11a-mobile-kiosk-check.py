from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks: list[tuple[str, bool]] = []


def read(path: str) -> str:
    return (root / path).read_text(encoding="utf-8")


def exists(path: str) -> bool:
    return (root / path).exists()


def add(name: str, ok: bool):
    checks.append((name, ok))


csproj = read("apps/Garmetix.AttendanceKiosk/Garmetix.AttendanceKiosk.csproj")
maui_program = read("apps/Garmetix.AttendanceKiosk/MauiProgram.cs")
app = read("apps/Garmetix.AttendanceKiosk/App.xaml.cs")
page = read("apps/Garmetix.AttendanceKiosk/Views/KioskShellPage.cs")
api_client = read("apps/Garmetix.AttendanceKiosk/Services/KioskApiClient.cs")
queue = read("apps/Garmetix.AttendanceKiosk/Services/OfflinePunchQueue.cs")
models = read("apps/Garmetix.AttendanceKiosk/Models/KioskModels.cs")
attendance_endpoints = read("backend/Garmetix.Api/Attendance/AttendanceEndpoints.cs")
web_page = read("frontend/garmetix-web/pages/attendance/mobile-kiosk.vue")
attendance_composable = read("frontend/garmetix-web/composables/useAttendance.ts")
access = read("frontend/garmetix-web/composables/useAccessControl.ts")
app_shell = read("frontend/garmetix-web/components/AppShell.vue")
legacy_shell = read("frontend/garmetix-web/components/AppShellLegacy.vue")
app_info = read("backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs")
app_version = read("frontend/garmetix-web/utils/appVersion.ts")
api_project = read("backend/Garmetix.Api/Garmetix.Api.csproj")
readme = read("README.md")
roadmap = read("docs/planning/CURRENT-ROADMAP.md")
operations_doc = read("docs/operations/Stage11A-Mobile-Attendance-Kiosk-v4.11.0.md")
build_doc = read("docs/operations/Stage11A-Android-Build-Hardening-v4.11.1.md")
current_release = read("scripts/validation/current-release-checks.py")

mobile_files = [
    "apps/Garmetix.AttendanceKiosk/Garmetix.AttendanceKiosk.csproj",
    "apps/Garmetix.AttendanceKiosk/MauiProgram.cs",
    "apps/Garmetix.AttendanceKiosk/App.xaml.cs",
    "apps/Garmetix.AttendanceKiosk/Views/KioskShellPage.cs",
    "apps/Garmetix.AttendanceKiosk/Services/KioskApiClient.cs",
    "apps/Garmetix.AttendanceKiosk/Services/OfflinePunchQueue.cs",
    "apps/Garmetix.AttendanceKiosk/Models/KioskModels.cs",
]

add("mobile shell files exist", all(exists(path) for path in mobile_files))
add(
    "version identity",
    all(token in app_info for token in ['Version = "4.11.1"', "Stage 11A Android Build Hardening", "GARMETIX-11A-20260621-4111"])
    and "APP_VERSION = '4.11.1'" in app_version
    and "Stage 11A Android Build Hardening" in app_version
    and "GARMETIX-11A-20260621-4111" in app_version
    and "<Version>4.11.1</Version>" in api_project,
)
add(
    "maui android shell contract",
    "net10.0-android" in csproj
    and "<UseMaui>true</UseMaui>" in csproj
    and "Microsoft.Data.Sqlite" in csproj
    and "Microsoft.Maui.Controls" in csproj
    and "<ApplicationDisplayVersion>4.11.1</ApplicationDisplayVersion>" in csproj
    and "<ApplicationVersion>4111</ApplicationVersion>" in csproj
    and "UseMauiApp<App>()" in maui_program
    and "KioskShellPage" in app
    and "CreateWindow" in app
    and "MainPage =" not in app
    and "KioskApiClient" in maui_program
    and "OfflinePunchQueue" in maui_program,
)
add(
    "offline queue and kiosk models",
    "pending_punches" in queue
    and "garmetix-kiosk-queue.db" in queue
    and "MarkSyncedAsync" in queue
    and "MarkRetryAsync" in queue
    and "PendingPunchRow" in models
    and "KioskPunchRequest" in models
    and "SyncPendingRequest" in models,
)
add(
    "kiosk page offline workflow",
    all(token in page for token in [
        "Check In",
        "Check Out",
        "Sync Pending",
        "PunchAsync",
        "EnqueueAsync",
        "LookupEmployeeAsync",
        "CheckReadinessAsync",
    ]),
)
add(
    "kiosk api client routes",
    all(token in api_client for token in [
        "attendance/kiosk/readiness",
        "attendance/kiosk/lookup-employee",
        "attendance/kiosk/punch",
        "attendance/kiosk/sync-pending",
        '}/api/{path.TrimStart(\'/\')}"',
    ]),
)
add(
    "attendance mobile status endpoints",
    'group.MapGet("/mobile-kiosk/status", MobileKioskStatusAsync)' in attendance_endpoints
    and 'group.MapGet("/mobile-kiosk/offline-contract", MobileKioskOfflineContractAsync)' in attendance_endpoints
    and "SQLite local pending_punches table" in attendance_endpoints
    and "buildCommand" in attendance_endpoints
    and "expectedArtifacts" in attendance_endpoints
    and "SQLitePCLRaw.lib.e_sqlite3.android" in attendance_endpoints
    and "Application.CreateWindow with NavigationPage root" in attendance_endpoints
    and "sync-pending" in attendance_endpoints
    and "photo-proof" in attendance_endpoints,
)
add(
    "frontend status page and composable",
    "attendance/mobile-kiosk/status" in attendance_composable
    and "attendance/mobile-kiosk/offline-contract" in attendance_composable
    and "Mobile Attendance Kiosk" in web_page
    and "SQLite offline queue" in web_page
    and "Android build profile" in web_page
    and "Package advisories" in web_page
    and "expectedArtifacts" in web_page
    and "acceptanceChecks" in web_page
    and "safetyRules" in web_page,
)
add(
    "route access and navigation",
    "path: '/attendance/mobile-kiosk'" in access
    and "/attendance/mobile-kiosk" in app_shell
    and "/attendance/mobile-kiosk" in legacy_shell
    and "stage 11a" in app_shell
    and "stage 11a" in legacy_shell,
)
add(
    "docs and current release validation",
    "stage11a-mobile-kiosk-check.py" in current_release
    and "Stage 11A Android Build Hardening" in readme
    and "Stage 11A Android Build Hardening" in roadmap
    and "GET /api/attendance/mobile-kiosk/status" in operations_doc
    and "GET /api/attendance/mobile-kiosk/offline-contract" in operations_doc,
)
add(
    "build hardening docs",
    "Application.CreateWindow" in build_doc
    and "com.garmetix.attendancekiosk-Signed.apk" in build_doc
    and "SQLitePCLRaw.lib.e_sqlite3.android 2.1.11" in build_doc
    and "Physical Tablet Rehearsal" in build_doc,
)

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(("PASS" if ok else "FAIL") + f": {name}")
if failed:
    raise SystemExit("Stage 11A mobile kiosk validation failed: " + ", ".join(failed))
print("Stage 11A mobile kiosk validation passed.")

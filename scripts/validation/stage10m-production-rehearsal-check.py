from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks: list[tuple[str, bool]] = []


def read(path: str) -> str:
    return (root / path).read_text(encoding="utf-8")


def add(name: str, ok: bool):
    checks.append((name, ok))


endpoint = read("backend/Garmetix.Api/Production/Stage10MProductionRehearsalEndpoints.cs")
program = read("backend/Garmetix.Api/Program.cs")
page = read("frontend/garmetix-web/pages/production-rehearsal/index.vue")
access = read("frontend/garmetix-web/composables/useAccessControl.ts")
app_shell = read("frontend/garmetix-web/components/AppShell.vue")
legacy_shell = read("frontend/garmetix-web/components/AppShellLegacy.vue")
app_info = read("backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs")
app_version = read("frontend/garmetix-web/utils/appVersion.ts")
csproj = read("backend/Garmetix.Api/Garmetix.Api.csproj")
readme = read("README.md")
roadmap = read("docs/planning/CURRENT-ROADMAP.md")
operations_doc = read("docs/operations/Stage10M-Production-Rehearsal-v4.10.31.md")
current_release = read("scripts/validation/current-release-checks.py")

add(
    "version identity",
    (
        all(token in app_info for token in ['Version = "4.10.31"', "Stage 10M Production Rehearsal Tracker", "GARMETIX-10M-20260620-4131"])
        and "APP_VERSION = '4.10.31'" in app_version
        and "Stage 10M Production Rehearsal Tracker" in app_version
        and "GARMETIX-10M-20260620-4131" in app_version
        and "<Version>4.10.31</Version>" in csproj
    )
    or (
        all(token in app_info for token in ['Version = "4.11.0"', "Stage 11A MAUI Android Attendance Kiosk Shell", "GARMETIX-11A-20260621-4110"])
        and "APP_VERSION = '4.11.0'" in app_version
        and "Stage 11A MAUI Android Attendance Kiosk Shell" in app_version
        and "GARMETIX-11A-20260621-4110" in app_version
        and "<Version>4.11.0</Version>" in csproj
    )
    or (
        all(token in app_info for token in ['Version = "4.11.1"', "Stage 11A Android Build Hardening", "GARMETIX-11A-20260621-4111"])
        and "APP_VERSION = '4.11.1'" in app_version
        and "Stage 11A Android Build Hardening" in app_version
        and "GARMETIX-11A-20260621-4111" in app_version
        and "<Version>4.11.1</Version>" in csproj
    )
    or (
        all(token in app_info for token in ['Version = "4.11.11"', "Stage 11B-9 Mantra Service Harness", "GARMETIX-11B-20260622-4121"])
        and "APP_VERSION = '4.11.11'" in app_version
        and "Stage 11B-9 Mantra Service Harness" in app_version
        and "GARMETIX-11B-20260622-4121" in app_version
        and "<Version>4.11.11</Version>" in csproj
    ),
)
add(
    "backend endpoint registered",
    "MapStage10MProductionRehearsalEndpoints" in endpoint
    and '"/api/stage10m/production-rehearsal"' in endpoint
    and "group.MapGet(\"\", Summary)" in endpoint
    and 'group.MapGet("/run-sheet", RunSheet)' in endpoint
    and ".RequireAuthorization(GarmetixPolicies.Admin)" in endpoint
    and "app.MapStage10MProductionRehearsalEndpoints();" in program,
)
add(
    "rehearsal phases cover production flows",
    all(token in endpoint for token in [
        "Pre-flight and workspace",
        "Store day and cash opening",
        "Billing and document scan",
        "Purchase, stock and import/export",
        "Accounting, banking and voucher print",
        "HR, attendance and payroll",
        "Close, support and go/no-go",
        "Save or validation failure",
        "Print or QR failure",
        "Hosted API or tunnel mismatch",
        "/production-support",
    ]),
)
add(
    "frontend page loads rehearsal summary and run sheet",
    all(token in page for token in [
        "stage10m/production-rehearsal",
        "stage10m/production-rehearsal/run-sheet",
        "Production Rehearsal",
        "Live-data run sheet",
        "Blocking checks",
        "Issue buckets",
        "Printable run sheet",
        "go/no-go evidence",
    ]),
)
add(
    "route access and navigation",
    "path: '/production-rehearsal'" in access
    and "Production Rehearsal" in app_shell
    and "Production Rehearsal" in legacy_shell
    and "stage 10m" in app_shell
    and "stage 10m" in legacy_shell,
)
add(
    "docs and release validation",
    "stage10m-production-rehearsal-check.py" in current_release
    and "Stage 10M Production Rehearsal Tracker" in readme
    and "Stage 10M Production Rehearsal Tracker" in roadmap
    and "GET /api/stage10m/production-rehearsal" in operations_doc,
)

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(("PASS" if ok else "FAIL") + f": {name}")
if failed:
    raise SystemExit("Stage 10M production rehearsal validation failed: " + ", ".join(failed))
print("Stage 10M production rehearsal validation passed.")

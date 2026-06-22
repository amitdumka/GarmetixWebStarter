from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks: list[tuple[str, bool]] = []


def read(path: str) -> str:
    return (root / path).read_text(encoding="utf-8")


def add(name: str, ok: bool):
    checks.append((name, ok))


endpoint = read("backend/Garmetix.Api/Production/Stage10LProductionSupportEndpoints.cs")
program = read("backend/Garmetix.Api/Program.cs")
page = read("frontend/garmetix-web/pages/production-support/index.vue")
access = read("frontend/garmetix-web/composables/useAccessControl.ts")
app_shell = read("frontend/garmetix-web/components/AppShell.vue")
legacy_shell = read("frontend/garmetix-web/components/AppShellLegacy.vue")
app_info = read("backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs")
app_version = read("frontend/garmetix-web/utils/appVersion.ts")
csproj = read("backend/Garmetix.Api/Garmetix.Api.csproj")
readme = read("README.md")
roadmap = read("docs/planning/CURRENT-ROADMAP.md")
operations_doc = read("docs/operations/Stage10L-Production-Support-v4.10.30.md")
current_release = read("scripts/validation/current-release-checks.py")

add(
    "version identity",
    (
        all(token in app_info for token in ['Version = "4.10.30"', "Stage 10L Production Support Pack", "GARMETIX-10L-20260620-4130"])
        and "APP_VERSION = '4.10.30'" in app_version
        and "Stage 10L Production Support Pack" in app_version
        and "GARMETIX-10L-20260620-4130" in app_version
        and "<Version>4.10.30</Version>" in csproj
    )
    or (
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
        all(token in app_info for token in ['Version = "4.11.7"', "Stage 11B-5 Fingerprint Kiosk Punch Guard", "GARMETIX-11B-20260621-4117"])
        and "APP_VERSION = '4.11.7'" in app_version
        and "Stage 11B-5 Fingerprint Kiosk Punch Guard" in app_version
        and "GARMETIX-11B-20260621-4117" in app_version
        and "<Version>4.11.7</Version>" in csproj
    ),
)
add(
    "backend endpoint registered",
    "MapStage10LProductionSupportEndpoints" in endpoint
    and '"/api/stage10l/production-support"' in endpoint
    and "group.MapGet(\"\", Summary)" in endpoint
    and 'group.MapGet("/drills", Drills)' in endpoint
    and ".RequireAuthorization(GarmetixPolicies.Admin)" in endpoint
    and "app.MapStage10LProductionSupportEndpoints();" in program,
)
add(
    "support drills cover production failures",
    all(token in endpoint for token in [
        "Save failure drill",
        "Print or PDF failure drill",
        "Backup warning drill",
        "Email or share failure drill",
        "Tunnel or API mismatch drill",
        "X-Forwarded-Host",
        "X-Forwarded-Proto",
        "trycloudflare",
        "/message-logs",
        "/runtime-diagnostics",
        "/backup-maintenance",
        "/email-delivery",
    ]),
)
add(
    "frontend page loads support summary and drills",
    all(token in page for token in [
        "stage10l/production-support",
        "stage10l/production-support/drills",
        "Production Support",
        "failed save",
        "failed print",
        "backup warning",
        "email/share failure",
        "hosted API mismatch",
        "quickLinks",
    ]),
)
add(
    "route access and navigation",
    "path: '/production-support'" in access
    and "Production Support" in app_shell
    and "Production Support" in legacy_shell
    and "stage 10l" in app_shell
    and "stage 10l" in legacy_shell,
)
add(
    "docs and release validation",
    "stage10l-production-support-check.py" in current_release
    and "Stage 10L Production Support Pack" in readme
    and "Stage 10L Production Support Pack" in roadmap
    and "GET /api/stage10l/production-support" in operations_doc,
)

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(("PASS" if ok else "FAIL") + f": {name}")
if failed:
    raise SystemExit("Stage 10L production support validation failed: " + ", ".join(failed))
print("Stage 10L production support validation passed.")

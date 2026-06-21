from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks: list[tuple[str, bool]] = []


def read(path: str) -> str:
    return (root / path).read_text(encoding="utf-8")


def add(name: str, ok: bool):
    checks.append((name, ok))


endpoint = read("backend/Garmetix.Api/Production/Stage10KOperatorAcceptanceEndpoints.cs")
program = read("backend/Garmetix.Api/Program.cs")
page = read("frontend/garmetix-web/pages/stage10k-operator-acceptance/index.vue")
access = read("frontend/garmetix-web/composables/useAccessControl.ts")
app_shell = read("frontend/garmetix-web/components/AppShell.vue")
legacy_shell = read("frontend/garmetix-web/components/AppShellLegacy.vue")
app_info = read("backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs")
app_version = read("frontend/garmetix-web/utils/appVersion.ts")
csproj = read("backend/Garmetix.Api/Garmetix.Api.csproj")
readme = read("README.md")
roadmap = read("docs/planning/CURRENT-ROADMAP.md")
operations_doc = read("docs/operations/Stage10K-Operator-Acceptance-v4.10.29.md")
current_release = read("scripts/validation/current-release-checks.py")

add(
    "version identity",
    (
        all(token in app_info for token in ['Version = "4.10.29"', "Stage 10K Production Operator Acceptance", "GARMETIX-10K-20260620-4129"])
        and "APP_VERSION = '4.10.29'" in app_version
        and "Stage 10K Production Operator Acceptance" in app_version
        and "GARMETIX-10K-20260620-4129" in app_version
        and "<Version>4.10.29</Version>" in csproj
    )
    or (
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
        all(token in app_info for token in ['Version = "4.11.5"', "Stage 11B-3 External Fingerprint Bridge Connector", "GARMETIX-11B-20260621-4115"])
        and "APP_VERSION = '4.11.5'" in app_version
        and "Stage 11B-3 External Fingerprint Bridge Connector" in app_version
        and "GARMETIX-11B-20260621-4115" in app_version
        and "<Version>4.11.5</Version>" in csproj
    ),
)
add(
    "backend endpoint registered",
    "MapStage10KOperatorAcceptanceEndpoints" in endpoint
    and '"/api/stage10k/operator-acceptance"' in endpoint
    and "group.MapGet(\"\", Summary)" in endpoint
    and 'group.MapGet("/checklist", Checklist)' in endpoint
    and ".RequireAuthorization(GarmetixPolicies.Admin)" in endpoint
    and "app.MapStage10KOperatorAcceptanceEndpoints();" in program,
)
add(
    "operator sections cover daily store operations",
    all(token in endpoint for token in [
        "Day opening and store readiness",
        "Billing and sales desk",
        "Cash closing and petty cash",
        "Purchase and inventory",
        "Voucher, accounting and banking",
        "HR attendance and payroll",
        "Backup, restore and support",
        "/store-day",
        "/billing/new",
        "/petty-cash",
        "/vouchers",
        "/attendance/today",
        "/backup-maintenance",
    ]),
)
add(
    "frontend page loads summary and checklist",
    all(token in page for token in [
        "stage10k/operator-acceptance",
        "stage10k/operator-acceptance/checklist",
        "Production Operator Acceptance",
        "quickLinks",
        "/document-scan",
        "/message-logs",
        "Check Message Logs",
    ]),
)
add(
    "route access and navigation",
    "path: '/stage10k-operator-acceptance'" in access
    and "Stage 10K Operator Acceptance" in app_shell
    and "Stage 10K Operator Acceptance" in legacy_shell
    and "stage 10k" in app_shell
    and "stage 10k" in legacy_shell,
)
add(
    "docs and release validation",
    "stage10k-operator-acceptance-check.py" in current_release
    and "Stage 10K Production Operator Acceptance" in readme
    and "Stage 10K Production Operator Acceptance" in roadmap
    and "GET /api/stage10k/operator-acceptance" in operations_doc,
)

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(("PASS" if ok else "FAIL") + f": {name}")
if failed:
    raise SystemExit("Stage 10K operator acceptance validation failed: " + ", ".join(failed))
print("Stage 10K operator acceptance validation passed.")

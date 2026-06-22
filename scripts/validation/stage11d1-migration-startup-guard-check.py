from pathlib import Path

root = Path(__file__).resolve().parents[2]
failures = []


def read(path: str) -> str:
    return (root / path).read_text(encoding="utf-8")


def add(name: str, ok: bool) -> None:
    print(("PASS" if ok else "FAIL") + f" - {name}")
    if not ok:
        failures.append(name)


program = read("backend/Garmetix.Api/Program.cs")
app_info = read("backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs")
app_version = read("frontend/garmetix-web/utils/appVersion.ts")
api_project = read("backend/Garmetix.Api/Garmetix.Api.csproj")
kiosk_project = read("apps/Garmetix.AttendanceKiosk/Garmetix.AttendanceKiosk.csproj")
bridge_project = read("apps/Garmetix.FingerprintBridge/Garmetix.FingerprintBridge.csproj")
mock_project = read("apps/Garmetix.MantraMockService/Garmetix.MantraMockService.csproj")
readme = read("README.md")
roadmap = read("docs/planning/CURRENT-ROADMAP.md")

add(
    "release identity v4.11.16",
    all(token in app_info for token in ['Version = "4.11.16"', "Stage 11D-1 Migration Startup Guard", "GARMETIX-11D1-20260622-4116"])
    and "APP_VERSION = '4.11.16'" in app_version
    and "Stage 11D-1 Migration Startup Guard" in app_version
    and "GARMETIX-11D1-20260622-4116" in app_version
    and "<Version>4.11.16</Version>" in api_project
    and "<ApplicationDisplayVersion>4.11.16</ApplicationDisplayVersion>" in kiosk_project
    and "<ApplicationVersion>4116</ApplicationVersion>" in kiosk_project
    and "<Version>4.11.16</Version>" in bridge_project
    and "<Version>4.11.16</Version>" in mock_project,
)
add("startup uses guarded migration helper", "ApplyDatabaseStartupMigrationsAsync" in program and "MarkFreshBaselineForExistingSchemaAsync" in program)
add("reset is explicit only", "GARMETIX_RESET_DATABASE" in program and "Database:ResetOnStartup" in program and "EnsureDeletedAsync" in program)
add("existing schema baseline is conservative", "existingTableCount < 40" in program and '["Users", "Companies", "Stores", "AttendanceApprovals", "Ledgers", "LedgerGroups", "Employees", "Products", "SalesInvoices", "Vouchers"]' in program)
add("baseline marker inserted with conflict safety", "InsertBaselineMigrationHistoryAsync" in program and 'ON CONFLICT ("MigrationId") DO NOTHING' in program)
add("migration diagnostics logged", "Database provider:" in program and "Database migration status before migrate" in program and "Pending:" in program)
add("documentation records startup guard", "Stage 11D-1 Migration Startup Guard" in readme and "GARMETIX_RESET_DATABASE=true" in readme and "Current version: 4.11.16" in roadmap)

if failures:
    raise SystemExit("Stage 11D-1 migration startup guard validation failed: " + ", ".join(failures))

print("Stage 11D-1 migration startup guard validation passed.")

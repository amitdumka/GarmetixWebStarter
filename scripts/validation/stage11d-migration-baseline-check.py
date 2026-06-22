from pathlib import Path

root = Path(__file__).resolve().parents[2]
failures = []


def read(path: str) -> str:
    return (root / path).read_text(encoding="utf-8")


def exists(path: str) -> bool:
    return (root / path).exists()


def add(name: str, ok: bool) -> None:
    print(("PASS" if ok else "FAIL") + f" - {name}")
    if not ok:
        failures.append(name)


migration_dir = root / "backend/Garmetix.Infrastructure/Data/Migrations"
migration_files = sorted(path.name for path in migration_dir.glob("*.cs"))
migration_code_files = [name for name in migration_files if not name.endswith(".Designer.cs") and name != "GarmetixDbContextModelSnapshot.cs"]
initial_code = read("backend/Garmetix.Infrastructure/Data/Migrations/20260622145928_Initial.cs")
snapshot = read("backend/Garmetix.Infrastructure/Data/Migrations/GarmetixDbContextModelSnapshot.cs")
app_user = read("backend/Garmetix.Domain/Generated/Models/Authentication/AppUser.cs")
system_defaults = read("backend/Garmetix.Api/Setup/SystemDefaultsService.cs")
program = read("backend/Garmetix.Api/Program.cs")
users = read("backend/Garmetix.Api/Auth/UserManagementEndpoints.cs")
token = read("backend/Garmetix.Api/Auth/JwtTokenService.cs")
workspace = read("backend/Garmetix.Api/Workspace/WorkspaceScope.cs")
factory_reset = read("backend/Garmetix.Api/Backup/FactoryResetEndpoints.cs")
app_info = read("backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs")
app_version = read("frontend/garmetix-web/utils/appVersion.ts")
csproj = read("backend/Garmetix.Api/Garmetix.Api.csproj")

add("single fresh Initial migration", migration_code_files == ["20260622145928_Initial.cs"])
add("migration snapshot includes super admin flag", 'Property<bool>("IsSuperAdmin")' in snapshot and "IsSuperAdmin = table.Column<bool>" in initial_code)
add("AppUser has internal super-admin flag", "public bool IsSuperAdmin" in app_user)
add("startup seeds system defaults", "EnsureStartupDefaultsAsync" in program and "AddScoped<SystemDefaultsService>" in program)
add("fresh schema marker matches Initial migration", 'FreshSchemaBaselineMigrationId = "20260622145928_Initial"' in program)
add("super admin seeded as garmetix", 'SuperAdminUserName = "garmetix"' in system_defaults and "EnsureSuperAdminAsync" in system_defaults)
add("Indian accounting defaults protected and seeded", "Capital Account" in system_defaults and "Sundry Debtors" in system_defaults and "Duties & Taxes" in system_defaults)
add("Manager salesman defaults are automatic", "EnsureManagerSalesmanForStoreAsync" in system_defaults and 'Name = "Manager"' in system_defaults)
add("normal admins cannot list super admin", "!user.IsSuperAdmin" in users and "IsCurrentUserSuperAdmin" in users)
add("super admin claim reaches token and workspace scope", 'new("superAdmin"' in token and 'FindFirst("superAdmin")' in workspace)
add("factory reset recreates super admin", "EnsureSuperAdminAsync" in factory_reset and "Garmetix super admin was recreated" in factory_reset)
add("release identity v4.11.16", 'Version = "4.11.16"' in app_info and "GARMETIX-11D1-20260622-4116" in app_info and "APP_VERSION = '4.11.16'" in app_version and "<Version>4.11.16</Version>" in csproj)

if failures:
    raise SystemExit("Stage 11D migration baseline validation failed: " + ", ".join(failures))

print("Stage 11D migration baseline validation passed.")

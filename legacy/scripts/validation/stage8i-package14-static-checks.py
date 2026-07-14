from pathlib import Path
import json

ROOT = Path(__file__).resolve().parents[2]
checks = []

def text(path: str) -> str:
    return (ROOT / path).read_text()

def exists(path: str) -> bool:
    return (ROOT / path).exists()

def add(name: str, condition: bool):
    checks.append((name, bool(condition)))

endpoints = text('backend/Garmetix.Api/Dashboard/DashboardEndpoints.cs')
workspace = text('backend/Garmetix.Api/Workspace/WorkspaceScope.cs')
release = text('backend/Garmetix.Api/Release/ReleaseStabilizationEndpoints.cs')
catalog = text('backend/Garmetix.Api/Testing/TestAutomationCatalog.cs')
testing = text('backend/Garmetix.Api/Testing/TestAutomationEndpoints.cs')
linux_smoke = text('scripts/linux/smoke-test.sh')
windows_smoke = text('scripts/windows/smoke-test.ps1')
app_version = text('frontend/garmetix-web/utils/appVersion.ts')
backend_version = text('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs')
backend_project = text('backend/Garmetix.Api/Garmetix.Api.csproj')
roadmap = text('docs/planning/CURRENT-ROADMAP.md')
issues = text('docs/planning/ISSUES.md')
pkg = json.loads(text('frontend/garmetix-web/package.json'))
lock = json.loads(text('frontend/garmetix-web/package-lock.json'))

add('dashboard home endpoint maps HomeAsync', 'MapGet("/home", HomeAsync)' in endpoints)
add('dashboard home returns explicit Ok DTO', 'Task.FromResult<IResult>(Results.Ok(ResolveHome(context.User)))' in endpoints)
add('dashboard ResolveHome is public/testable', 'public static DashboardHomeDto ResolveHome(ClaimsPrincipal user)' in endpoints)
add('dashboard home old direct DTO handler removed', 'private static DashboardHomeDto Home(HttpContext context)' not in endpoints)
add('workspace principal full-access overload added', 'public static bool HasFullAccess(ClaimsPrincipal? principal)' in workspace)
add('dashboard home routing tests added', exists('backend/Garmetix.Api.Tests/Dashboard/DashboardHomeRoutingTests.cs') and 'DashboardHomeRoutingTests' in text('backend/Garmetix.Api.Tests/Dashboard/DashboardHomeRoutingTests.cs'))
add('release smoke dashboard contract check added', 'DASHBOARD_HOME_CONTRACT' in release and 'AddDashboardHomeContractCheck(checks)' in release)
add('test automation manifest dashboard code added', '"DASHBOARD_HOME_CONTRACT"' in catalog and 'DashboardHomeRoutingTests' in catalog)
add('runtime smoke requires dashboard code', '"DASHBOARD_HOME_CONTRACT"' in testing)
add('linux smoke expected package14 version', 'GARMETIX_EXPECTED_VERSION:-4.9.13' in linux_smoke and 'GARMETIX-8I-20260619-49130' in linux_smoke)
add('linux smoke validates authenticated dashboard/home', '$API_BASE/dashboard/home' in linux_smoke and 'dashboardType' in linux_smoke)
add('windows smoke expected package14 version', 'ExpectedVersion = "4.9.13"' in windows_smoke and 'GARMETIX-8I-20260619-49130' in windows_smoke)
add('windows smoke validates authenticated dashboard/home', '$ApiBase/dashboard/home' in windows_smoke and 'dashboardType' in windows_smoke)
add('frontend package version 4.9.13', pkg.get('version') == '4.9.13')
add('frontend lock version 4.9.13', lock.get('version') == '4.9.13' and lock.get('packages', {}).get('', {}).get('version') == '4.9.13')
add('frontend app version package14', "APP_VERSION = '4.9.13'" in app_version and 'GARMETIX-8I-20260619-49130' in app_version)
add('backend app version package14', 'Version = "4.9.13"' in backend_version and 'GARMETIX-8I-20260619-49130' in backend_version)
add('backend project version package14', '<Version>4.9.13</Version>' in backend_project and '<AssemblyVersion>4.9.13.0</AssemblyVersion>' in backend_project)
add('operation docs added', exists('docs/operations/Dashboard-Home-Smoke-Hardening-v4.9.13.md'))
add('stage notes added', exists('docs/stages/stage-8/Stage8I-Package14-DashboardHomeSmokeHardening-v4.9.13-Notes.md'))
add('roadmap updated for package14', 'Stage 8I Package 14 Dashboard Home Smoke Hardening / v4.9.13' in roadmap)
add('issues marks dashboard bug fixed', 'fixed in v4.9.13' in issues and 'Dashboard home endpoint contract fixed in v4.9.13' in issues)

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(f"{'PASS' if ok else 'FAIL'}: {name}")

if failed:
    raise SystemExit(f"Failed checks: {', '.join(failed)}")

print('\nStage 8I Package 14 static checks passed.')

from pathlib import Path

root = Path(__file__).resolve().parents[2]

required_files = {
    'Test automation DTOs': root / 'backend/Garmetix.Api/Testing/TestAutomationDtos.cs',
    'Test automation catalog': root / 'backend/Garmetix.Api/Testing/TestAutomationCatalog.cs',
    'Test automation endpoints': root / 'backend/Garmetix.Api/Testing/TestAutomationEndpoints.cs',
    'Test automation catalog tests': root / 'backend/Garmetix.Api.Tests/Testing/TestAutomationCatalogTests.cs',
    'Linux run automated tests': root / 'scripts/linux/run-automated-tests.sh',
    'Linux docker smoke test': root / 'scripts/linux/docker-smoke-test.sh',
    'Linux API smoke test': root / 'scripts/linux/smoke-test.sh',
    'Windows run automated tests': root / 'scripts/windows/run-automated-tests.ps1',
    'Windows API smoke test': root / 'scripts/windows/smoke-test.ps1',
    'Frontend smoke script': root / 'frontend/garmetix-web/scripts/frontend-smoke.mjs',
    'Stage notes': root / 'docs/stages/stage-8/Stage8F-Package2-AutomatedSmokeTests-v4.5.1-Notes.md',
}

missing = [name for name, path in required_files.items() if not path.exists()]
if missing:
    raise SystemExit(f'Missing Stage 8F Package 2 files: {missing}')

program = (root / 'backend/Garmetix.Api/Program.cs').read_text()
for token in ['using Garmetix.Api.Testing;', 'app.MapTestAutomationEndpoints();']:
    if token not in program:
        raise SystemExit(f'Program.cs missing {token}')

catalog = required_files['Test automation catalog'].read_text()
for token in [
    'BACKEND_UNIT_TESTS',
    'FRONTEND_BUILD',
    'FRONTEND_SMOKE',
    'DOCKER_COMPOSE_BUILD',
    'DOCKER_HEALTH',
    'AUTHENTICATED_API_SMOKE',
]:
    if token not in catalog:
        raise SystemExit(f'Test automation catalog missing {token}')

endpoints = required_files['Test automation endpoints'].read_text()
for token in ['MapGet("/manifest"', 'MapGet("/runtime-smoke"', 'Database.CanConnectAsync', 'AuditLogEntries', 'TestAutomationRuntimeSummaryDto']:
    if token not in endpoints:
        raise SystemExit(f'Test automation endpoints missing {token}')

linux_runner = required_files['Linux run automated tests'].read_text()
for token in ['dotnet test backend/Garmetix.Api.Tests/Garmetix.Api.Tests.csproj', 'npm ci', 'npm run build', 'RUN_DOCKER_SMOKE', 'stage8f-package2-static-checks.py']:
    if token not in linux_runner:
        raise SystemExit(f'Linux automated test runner missing {token}')

linux_smoke = required_files['Linux API smoke test'].read_text()
for token in ['test-automation/manifest', 'test-automation/runtime-smoke', 'GARMETIX_EXPECTED_VERSION', 'GARMETIX_EXPECTED_BUILD_CODE']:
    if token not in linux_smoke:
        raise SystemExit(f'Linux smoke script missing {token}')

front_smoke = required_files['Frontend smoke script'].read_text()
for token in ['GARMETIX_WEB_BASE_URL', '/api/app-info/version', '/api/test-automation/manifest', 'FRONTEND_SMOKE']:
    if token not in front_smoke:
        raise SystemExit(f'Frontend smoke script missing {token}')

package_json = (root / 'frontend/garmetix-web/package.json').read_text()
if '"smoke:frontend": "node scripts/frontend-smoke.mjs"' not in package_json :
    raise SystemExit('Frontend package.json missing smoke script or v4.6.2 version')

app_version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
if "APP_VERSION = '4." not in app_version:
    raise SystemExit('Frontend app version not compatible with Stage 8G Package 3 or later')

backend_csproj = (root / 'backend/Garmetix.Api/Garmetix.Api.csproj').read_text()
if '<Version>4.' not in backend_csproj:
    raise SystemExit('Backend version not compatible with Stage 8G Package 3 or later')

app_info = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
if 'Version = "4.' not in app_info:
    raise SystemExit('AppInfo version not compatible with Stage 8G Package 3 or later')

roadmap = (root / 'docs/planning/CURRENT-ROADMAP.md').read_text()
if 'Stage 8G Package 3 SMTP Email Delivery Validation / v4.6.2' not in roadmap:
    raise SystemExit('Current roadmap missing Stage 8G Package 3 entry')

print('Stage 8F Package 2 static validation passed.')

from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks: list[tuple[str, bool]] = []

def read(path: str) -> str:
    return (root / path).read_text(encoding='utf-8')

def add(name: str, ok: bool):
    checks.append((name, ok))

app_info = read('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs')
app_version = read('frontend/garmetix-web/utils/appVersion.ts')
csproj = read('backend/Garmetix.Api/Garmetix.Api.csproj')
program = read('backend/Garmetix.Api/Program.cs')
endpoint = read('backend/Garmetix.Api/Production/Stage10AFinalAcceptanceEndpoints.cs')
page = read('frontend/garmetix-web/pages/production-final-acceptance/index.vue')
shell = read('frontend/garmetix-web/components/AppShell.vue')
access = read('frontend/garmetix-web/composables/useAccessControl.ts')
manifest = read('backend/Garmetix.Api/Testing/TestAutomationCatalog.cs')
runtime = read('backend/Garmetix.Api/Testing/TestAutomationEndpoints.cs')
linux_drill = read('scripts/linux/stage10a-production-final-acceptance-drill.sh')
current_release = read('scripts/validation/current-release-checks.py')

add('version identity', all(token in app_info for token in ['Version = "4.10.12"', 'Stage 10B Excel Import Export Center', 'GARMETIX-10B-20260620-4112']) and "APP_VERSION = '4.10.12'" in app_version and '<Version>4.10.12</Version>' in csproj)
add('app version safe quotes', 'APP_STAGE = "Stage 10B Excel Import Export Center"' in app_version and 'APP_RELEASE_NAME = "Stage 10B Production Final Acceptance Hotfix and Excel Import Export Center"' in app_version)
add('stage10a endpoint mapped', 'MapStage10AFinalAcceptanceEndpoints' in program and 'MapStage10AFinalAcceptanceEndpoints(this WebApplication app)' in endpoint)
add('stage10a endpoint contract', all(token in endpoint for token in ['/api/stage10a/final-acceptance', 'RequiredManifestCodes', 'missingManifestCodes', 'overallStatus', 'BuildSections', 'CreatePayload']))
add('runtime smoke build prefix fixed', 'BuildCode.StartsWith("GARMETIX-", StringComparison.Ordinal)' in runtime and 'GARMETIX-8' not in runtime)
add('test manifest includes stage10a', 'STAGE10A_FINAL_ACCEPTANCE' in manifest and 'stage10a-production-final-acceptance-drill.sh' in manifest)
add('frontend page exists', all(token in page for token in ['Production Final Acceptance', 'stage10a/final-acceptance', 'production-readiness/summary', 'test-automation/runtime-smoke', 'Today&apos;s dashboard', 'Promise.allSettled']))
add('menu and route access exist', "to: '/production-final-acceptance'" in shell and "path: '/production-final-acceptance'" in access)
add('linux host drill exists', all(token in linux_drill for token in ['stage10a/final-acceptance', 'dashboard/todays', 'attendance/final-acceptance', 'GARMETIX-10B-20260620-4112']))
add('current release invokes stage10a', 'stage10b-import-export-center-check.py' in current_release)

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(('PASS' if ok else 'FAIL') + f': {name}')
if failed:
    raise SystemExit('Stage 10A production final acceptance validation failed: ' + ', '.join(failed))
print('Stage 10A production final acceptance validation passed.')

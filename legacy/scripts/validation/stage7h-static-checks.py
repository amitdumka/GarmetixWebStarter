from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[2]
checks = []

def check(name, condition, detail=''):
    checks.append((name, bool(condition), detail))

app_version = root / 'frontend/garmetix-web/utils/appVersion.ts'
app_info = root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs'
app_dtos = root / 'backend/Garmetix.Api/AppInfo/AppInfoDtos.cs'
api_csproj = root / 'backend/Garmetix.Api/Garmetix.Api.csproj'
system_page = root / 'frontend/garmetix-web/pages/system-info/index.vue'
access = root / 'frontend/garmetix-web/composables/useAccessControl.ts'
shell = root / 'frontend/garmetix-web/components/AppShell.vue'
map_page = root / 'frontend/garmetix-web/pages/dashboard/map/index.vue'
notes = root / 'docs/stages/stage-7/Stage7H-System-Info-Route-Audit-Notes.md'

for path in [app_version, app_info, app_dtos, api_csproj, system_page, access, shell, map_page, notes]:
    check(f'{path.relative_to(root)} exists', path.exists())

if app_version.exists():
    text = app_version.read_text()
    check('frontend version is 3.7.0', "APP_VERSION = '3.7.0'" in text)
    check('frontend stage is Stage 7H', "APP_STAGE = 'Stage 7H'" in text)
    check('frontend build code is Stage 7H', 'GARMETIX-7H-20260610-370' in text)

if app_info.exists():
    text = app_info.read_text()
    check('backend version is 3.7.0', 'Version = "3.7.0"' in text)
    check('backend stage is Stage 7H', 'Stage = "Stage 7H"' in text)
    check('backend build code is Stage 7H', 'GARMETIX-7H-20260610-370' in text)
    check('system endpoint is mapped', 'group.MapGet("/system", SystemInfo)' in text)
    check('SystemInfo method exists', 'private static IResult SystemInfo' in text)

if app_dtos.exists():
    text = app_dtos.read_text()
    check('AppSystemInfoDto exists', 'AppSystemInfoDto' in text and 'UptimeSeconds' in text)

if api_csproj.exists():
    text = api_csproj.read_text()
    check('api csproj version updated', '<Version>3.7.0</Version>' in text)
    check('api csproj informational version updated', '<InformationalVersion>3.7.0-stage7h</InformationalVersion>' in text)

if system_page.exists():
    text = system_page.read_text()
    check('system page calls app-info/version', "api.get<any>('app-info/version')" in text)
    check('system page calls app-info/system', "api.get<any>('app-info/system')" in text)
    check('system page shows route audit', 'Dashboard route audit' in text and 'routeRows' in text)
    check('system page shows rollback reminder', 'NUXT_PUBLIC_DASHBOARD_SHELL=legacy' in text)
    check('system page has balanced template tags', text.count('\n<template>') == 1 and text.count('\n</template>') == 1)

if access.exists():
    text = access.read_text()
    check('system-info access rule exists', "{ path: '/system-info'" in text)

if shell.exists():
    text = shell.read_text()
    check('AppShell links system-info', "to: '/system-info'" in text)
    check('AppShell sidebar version label updated', 'Dashboard shell · v3.7' in text)
    check('legacy shell revert preserved', 'NUXT_PUBLIC_DASHBOARD_SHELL' in text or 'dashboardShell' in text)

if map_page.exists():
    text = map_page.read_text()
    check('dashboard map includes system info', "to: '/system-info'" in text)
    check('dashboard map mentions Stage 7H', 'Stage 7H' in text)

failed = [name for name, ok, _ in checks if not ok]
for name, ok, detail in checks:
    print(('PASS' if ok else 'FAIL') + f' - {name}' + (f' ({detail})' if detail else ''))

if failed:
    print('\nFAILED CHECKS:')
    for item in failed:
        print(f'- {item}')
    sys.exit(1)

print('\nStage 7H static validation passed.')

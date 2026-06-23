#!/usr/bin/env python3
from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
errors: list[str] = []


def read(rel: str) -> str:
    return (ROOT / rel).read_text(encoding='utf-8')


def exists(rel: str) -> bool:
    return (ROOT / rel).exists()


def add(name: str, ok: bool):
    if not ok:
        errors.append(name)

pkg = json.loads(read('frontend/garmetix-web/package.json'))
lock = json.loads(read('frontend/garmetix-web/package-lock.json'))
app_version = read('frontend/garmetix-web/utils/appVersion.ts')
backend_version = read('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs')
backend_project = read('backend/Garmetix.Api/Garmetix.Api.csproj')
catalog = read('backend/Garmetix.Api/Testing/TestAutomationCatalog.cs')
test_endpoints = read('backend/Garmetix.Api/Testing/TestAutomationEndpoints.cs')
linux_smoke = read('scripts/linux/smoke-test.sh')
windows_smoke = read('scripts/windows/smoke-test.ps1')
linux_drill = read('scripts/linux/docker-acceptance-drill.sh')
windows_drill = read('scripts/windows/docker-acceptance-drill.ps1')
roadmap = read('docs/planning/CURRENT-ROADMAP.md')
issues = read('docs/planning/ISSUES.md')
readme = read('README.md')
docs_readme = read('docs/README.md')

add('frontend package version 4.9.14', pkg.get('version') == '4.9.14')
add('frontend lock version 4.9.14', lock.get('version') == '4.9.14' and lock.get('packages', {}).get('', {}).get('version') == '4.9.14')
add('frontend app version package15', "APP_VERSION = '4.9.14'" in app_version and 'GARMETIX-8I-20260619-49140' in app_version)
add('backend app version package15', 'Version = "4.9.14"' in backend_version and 'Stage 8I Package 15 Docker Acceptance Drill' in backend_version and 'GARMETIX-8I-20260619-49140' in backend_version)
add('backend project version package15', '<Version>4.9.14</Version>' in backend_project and '<AssemblyVersion>4.9.14.0</AssemblyVersion>' in backend_project and '4.9.14-docker-acceptance-drill' in backend_project)
add('route access audit script exists', exists('scripts/validation/frontend-route-access-check.py'))
add('linux docker acceptance drill exists', exists('scripts/linux/docker-acceptance-drill.sh') and 'workspace/options' in linux_drill and 'docker compose' in linux_drill)
add('windows docker acceptance drill exists', exists('scripts/windows/docker-acceptance-drill.ps1') and 'workspace/options' in windows_drill and 'docker compose' in windows_drill)
add('manifest contains frontend route access audit', 'FRONTEND_ROUTE_ACCESS_AUDIT' in catalog and 'python3 scripts/validation/frontend-route-access-check.py' in catalog)
add('manifest contains docker acceptance drill', 'DOCKER_ACCEPTANCE_DRILL' in catalog and 'docker-acceptance-drill.sh' in catalog)
add('runtime required manifest updated', 'FRONTEND_ROUTE_ACCESS_AUDIT' in test_endpoints and 'DOCKER_ACCEPTANCE_DRILL' in test_endpoints)
add('linux smoke requires package15 manifest codes', 'FRONTEND_ROUTE_ACCESS_AUDIT' in linux_smoke and 'DOCKER_ACCEPTANCE_DRILL' in linux_smoke and 'GARMETIX_EXPECTED_VERSION:-4.9.14' in linux_smoke)
add('windows smoke requires package15 manifest codes', 'FRONTEND_ROUTE_ACCESS_AUDIT' in windows_smoke and 'DOCKER_ACCEPTANCE_DRILL' in windows_smoke and 'ExpectedVersion = "4.9.14"' in windows_smoke)
add('operation docs added', exists('docs/operations/Docker-Acceptance-Drill-v4.9.14.md'))
add('stage notes added', exists('docs/stages/stage-8/Stage8I-Package15-DockerAcceptanceDrill-v4.9.14-Notes.md'))
add('roadmap updated for package15', 'Stage 8I Package 15 Docker Acceptance Drill / v4.9.14' in roadmap)
add('issues notes docker drill coverage', 'Docker acceptance drill coverage added in v4.9.14' in issues)
add('readme current package updated', 'Stage 8I Package 15 Docker Acceptance Drill v4.9.14' in readme and 'GARMETIX-8I-20260619-49140' in readme)
add('docs readme current package updated', 'Stage 8I Package 15 Docker Acceptance Drill v4.9.14' in docs_readme and 'GARMETIX-8I-20260619-49140' in docs_readme)

route_audit = subprocess.run([sys.executable, str(ROOT / 'scripts/validation/frontend-route-access-check.py')], cwd=ROOT, text=True, capture_output=True)
if route_audit.returncode != 0:
    errors.append('frontend route access audit passes')
    print(route_audit.stdout)
    print(route_audit.stderr, file=sys.stderr)

if errors:
    print('Stage 8I Package 15 static checks failed:', file=sys.stderr)
    for error in errors:
        print(f' - {error}', file=sys.stderr)
    sys.exit(1)

print('Stage 8I Package 15 static checks passed.')

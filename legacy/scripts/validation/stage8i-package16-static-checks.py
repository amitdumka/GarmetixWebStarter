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
roadmap = read('docs/planning/CURRENT-ROADMAP.md')
issues = read('docs/planning/ISSUES.md')
readme = read('README.md')
docs_readme = read('docs/README.md')
app_vue = read('frontend/garmetix-web/app.vue')

add('frontend package version 4.9.15', pkg.get('version') == '4.9.15')
add('frontend lock version 4.9.15', lock.get('version') == '4.9.15' and lock.get('packages', {}).get('', {}).get('version') == '4.9.15')
add('frontend app version package16', "APP_VERSION = '4.9.15'" in app_version and 'GARMETIX-8I-20260619-49150' in app_version)
add('backend app version package16', 'Version = "4.9.15"' in backend_version and 'Stage 8I Package 16 Secret Hygiene and Hydration Guard' in backend_version and 'GARMETIX-8I-20260619-49150' in backend_version)
add('backend project version package16', '<Version>4.9.15</Version>' in backend_project and '<AssemblyVersion>4.9.15.0</AssemblyVersion>' in backend_project and '4.9.15-secret-hygiene-hydration-guard' in backend_project)
add('private deploy env removed', not exists('deploy/macmini.env'))
add('gitignore blocks private env', exists('.gitignore') and 'deploy/macmini.env' in read('.gitignore') and '.env.production' in read('.gitignore'))
add('secret hygiene script exists', exists('scripts/validation/secret-hygiene-check.py'))
add('hydration guard script exists', exists('scripts/validation/frontend-hydration-guard-check.py'))
add('app root client hydration guard', '<ClientOnly>' in app_vue and '<NuxtPage v-if="hydrated"' in app_vue and 'useAuth().restore()' in app_vue)
add('manifest contains secret hygiene audit', 'SECRET_HYGIENE_AUDIT' in catalog and 'secret-hygiene-check.py' in catalog)
add('manifest contains hydration guard audit', 'FRONTEND_HYDRATION_GUARD' in catalog and 'frontend-hydration-guard-check.py' in catalog)
add('runtime required manifest package16', 'SECRET_HYGIENE_AUDIT' in test_endpoints and 'FRONTEND_HYDRATION_GUARD' in test_endpoints)
add('linux smoke requires package16 manifest codes', 'SECRET_HYGIENE_AUDIT' in linux_smoke and 'FRONTEND_HYDRATION_GUARD' in linux_smoke and 'GARMETIX_EXPECTED_VERSION:-4.9.15' in linux_smoke)
add('windows smoke requires package16 manifest codes', 'SECRET_HYGIENE_AUDIT' in windows_smoke and 'FRONTEND_HYDRATION_GUARD' in windows_smoke and 'ExpectedVersion = "4.9.15"' in windows_smoke)
add('operation docs added', exists('docs/operations/Secret-Hygiene-Hydration-Guard-v4.9.15.md'))
add('stage notes added', exists('docs/stages/stage-8/Stage8I-Package16-SecretHygieneHydrationGuard-v4.9.15-Notes.md'))
add('roadmap updated for package16', 'Stage 8I Package 16 Secret Hygiene and Hydration Guard / v4.9.15' in roadmap)
add('issues updated for package16', 'Secret hygiene guard added in v4.9.15' in issues and 'Hydration guard added in v4.9.15' in issues)
add('readme current package updated', 'Stage 8I Package 16 Secret Hygiene and Hydration Guard v4.9.15' in readme and 'GARMETIX-8I-20260619-49150' in readme)
add('docs readme current package updated', 'Stage 8I Package 16 Secret Hygiene and Hydration Guard v4.9.15' in docs_readme and 'GARMETIX-8I-20260619-49150' in docs_readme)


if errors:
    print('Stage 8I Package 16 static checks failed:', file=sys.stderr)
    for error in errors:
        print(f' - {error}', file=sys.stderr)
    sys.exit(1)

print('Stage 8I Package 16 static checks passed.')

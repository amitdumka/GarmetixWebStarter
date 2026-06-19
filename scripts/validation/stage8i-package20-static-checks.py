#!/usr/bin/env python3
from __future__ import annotations

import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
errors: list[str] = []

def read(rel: str) -> str:
    return (ROOT / rel).read_text(encoding='utf-8')

def add(name: str, ok: bool):
    if not ok:
        errors.append(name)

# Keep route access audit inline for fast current-release validation.
pages_dir = ROOT / 'frontend/garmetix-web/pages'
access = read('frontend/garmetix-web/composables/useAccessControl.ts')
rules: list[tuple[str, bool]] = []
for match in re.finditer(r"\{\s*path:\s*'([^']+)'(?P<body>.*?)\}", access, flags=re.DOTALL):
    path = match.group(1).rstrip('/') or '/'
    exact = bool(re.search(r"exact:\s*true", match.group('body')))
    rules.append((path, exact))

def page_path(vue_file: Path) -> str:
    rel = vue_file.relative_to(pages_dir).with_suffix('')
    parts = list(rel.parts)
    if parts and parts[-1] == 'index':
        parts = parts[:-1]
    return ('/' + '/'.join(parts)).replace('//', '/') or '/'

def covered(path: str) -> bool:
    cleaned = path.rstrip('/') or '/'
    return any(
        (exact and cleaned == rule_path) or (not exact and (cleaned == rule_path or cleaned.startswith(f'{rule_path}/')))
        for rule_path, exact in rules
    )

approved = {'/access-denied', '/[module]'}
missing_routes = [path for path in sorted(page_path(f) for f in pages_dir.rglob('*.vue')) if path not in approved and not covered(path)]
add('frontend route access audit', not missing_routes and 'No explicit page rule is configured' in access)

pkg = json.loads(read('frontend/garmetix-web/package.json'))
lock = json.loads(read('frontend/garmetix-web/package-lock.json'))
app_version = read('frontend/garmetix-web/utils/appVersion.ts')
backend_version = read('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs')
backend_project = read('backend/Garmetix.Api/Garmetix.Api.csproj')
readme = read('README.md')
docs_readme = read('docs/README.md')
catalog = read('backend/Garmetix.Api/Testing/TestAutomationCatalog.cs')

add('frontend package version 4.9.19', pkg.get('version') == '4.9.19')
add('frontend lock version 4.9.19', lock.get('version') == '4.9.19' and lock.get('packages', {}).get('', {}).get('version') == '4.9.19')
add('frontend app version package20', "APP_VERSION = '4.9.19'" in app_version and 'Stage 8I Package 20 SMTP Email Integration and Delivery Acceptance' in app_version and 'GARMETIX-8I-20260619-49190' in app_version)
add('backend app version package20', 'Version = "4.9.19"' in backend_version and 'Stage 8I Package 20 SMTP Email Integration and Delivery Acceptance' in backend_version and 'GARMETIX-8I-20260619-49190' in backend_version)
add('backend project version package20', '<Version>4.9.19</Version>' in backend_project and '<AssemblyVersion>4.9.19.0</AssemblyVersion>' in backend_project and '4.9.19-smtp-email-delivery-acceptance' in backend_project)
add('manifest smtp acceptance', 'SMTP_DELIVERY_ACCEPTANCE' in catalog)
add('readme current package20', 'Stage 8I Package 20 SMTP Email Integration and Delivery Acceptance v4.9.19' in readme and 'GARMETIX-8I-20260619-49190' in readme)
add('docs readme current package20', 'Stage 8I Package 20 SMTP Email Integration and Delivery Acceptance v4.9.19' in docs_readme and 'GARMETIX-8I-20260619-49190' in docs_readme)

for script in ['smtp-delivery-acceptance-check.py', 'frontend-route-access-check.py', 'secret-hygiene-check.py']:
    try:
        subprocess.run([sys.executable, str(ROOT / 'scripts/validation' / script)], cwd=ROOT, check=True)
    except subprocess.CalledProcessError:
        errors.append(f'{script} subprocess')

if errors:
    print('Stage 8I Package 20 static checks failed:', file=sys.stderr)
    for error in errors:
        print(f' - {error}', file=sys.stderr)
    if missing_routes:
        print('Missing route rules:', ', '.join(missing_routes), file=sys.stderr)
    sys.exit(1)

print('Stage 8I Package 20 static checks passed.')

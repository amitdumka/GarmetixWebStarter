from pathlib import Path
import json
import re
import sys

ROOT = Path(__file__).resolve().parents[2]
checks = []
errors = []

def require(path, text, label):
    content = (ROOT / path).read_text(encoding='utf-8')
    if text not in content:
        errors.append(f"Missing {label}: {path} -> {text}")
    else:
        checks.append(label)

require('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs', 'public const string Version = "2.2.0";', 'backend app version')
require('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs', 'public const string BuildCode = "GARMETIX-6C-20260610-220";', 'backend build code')
require('frontend/garmetix-web/utils/appVersion.ts', "export const APP_VERSION = '2.2.0'", 'frontend app version')
require('frontend/garmetix-web/utils/appVersion.ts', "export const APP_BUILD_CODE = 'GARMETIX-6C-20260610-220'", 'frontend build code')
require('backend/Garmetix.Api/Program.cs', 'app.MapAppInfoEndpoints();', 'app info endpoint mapped')
require('frontend/garmetix-web/components/AppShell.vue', "label: 'Help'", 'help sidebar group')
require('frontend/garmetix-web/components/AppShell.vue', "to: '/about-us'", 'about link')
require('frontend/garmetix-web/components/AppShell.vue', "to: '/contact-us'", 'contact link')
require('frontend/garmetix-web/components/AppShell.vue', "to: '/faq'", 'faq link')

for page in ['about-us', 'contact-us', 'faq']:
    if not (ROOT / f'frontend/garmetix-web/pages/{page}/index.vue').exists():
        errors.append(f'Missing page: {page}')
    else:
        checks.append(f'{page} page exists')

package = json.loads((ROOT / 'frontend/garmetix-web/package.json').read_text(encoding='utf-8'))
if package.get('version') != '2.2.0':
    errors.append('frontend package.json version is not 2.2.0')
else:
    checks.append('package version')

api_csproj = (ROOT / 'backend/Garmetix.Api/Garmetix.Api.csproj').read_text(encoding='utf-8')
if '<Version>2.2.0</Version>' not in api_csproj:
    errors.append('API csproj version is not 2.2.0')
else:
    checks.append('api csproj version')

# basic Vue single-file-component guard for the new pages
for page in ['about-us', 'contact-us', 'faq']:
    text = (ROOT / f'frontend/garmetix-web/pages/{page}/index.vue').read_text(encoding='utf-8')
    if '<template>' not in text or '</template>' not in text:
        errors.append(f'{page} missing root template block')
    if '<script setup' not in text or '</script>' not in text:
        errors.append(f'{page} missing script setup block')

if errors:
    print('Stage 6C validation failed:')
    for error in errors:
        print(' -', error)
    sys.exit(1)

print(f'Stage 6C validation passed ({len(checks)} checks).')

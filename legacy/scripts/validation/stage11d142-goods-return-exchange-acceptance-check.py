#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
checks = []

def add(name, ok):
    checks.append((name, bool(ok)))

backend = ROOT / 'backend/Garmetix.Api/GoodsReturn/GoodsReturnAcceptanceEndpoints.cs'
program = ROOT / 'backend/Garmetix.Api/Program.cs'
page = ROOT / 'frontend/garmetix-web/pages/goods-return-acceptance/index.vue'
shell = ROOT / 'frontend/garmetix-web/components/AppShell.vue'
legacy = ROOT / 'frontend/garmetix-web/components/AppShellLegacy.vue'
access = ROOT / 'frontend/garmetix-web/composables/useAccessControl.ts'
app_info = ROOT / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs'
app_version = ROOT / 'frontend/garmetix-web/utils/appVersion.ts'
csproj = ROOT / 'backend/Garmetix.Api/Garmetix.Api.csproj'
release = ROOT / 'RELEASE-v4.12.57.txt'
doc_stage = ROOT / 'docs/stages/stage-11/Stage11D142-Goods-Return-Exchange-Acceptance-v4.12.57.md'
doc_module = ROOT / 'docs/modules/billing-sales/goods-return-exchange-acceptance-v4.12.57.md'

backend_text = backend.read_text() if backend.exists() else ''
program_text = program.read_text() if program.exists() else ''
page_text = page.read_text() if page.exists() else ''
shell_text = shell.read_text() if shell.exists() else ''
legacy_text = legacy.read_text() if legacy.exists() else ''
access_text = access.read_text() if access.exists() else ''
app_info_text = app_info.read_text() if app_info.exists() else ''
app_version_text = app_version.read_text() if app_version.exists() else ''
csproj_text = csproj.read_text() if csproj.exists() else ''

add('backend endpoint exists', backend.exists() and 'MapGoodsReturnAcceptanceEndpoints' in backend_text and '/api/goods-return/acceptance' in backend_text)
add('csv endpoint exists', 'evidence.csv' in backend_text and 'BuildCsv' in backend_text)
add('policy checks exist', all(token in backend_text for token in ['RETURN_OUTSIDE_7_DAY_POLICY', 'RETURN_REFUND_BLOCKED_BY_POLICY', 'RETURN_CREDIT_NOTE_EXPIRED_OPEN', 'CalculateCreditNoteExpiry']))
add('program mapped', 'using Garmetix.Api.GoodsReturn;' in program_text and 'app.MapGoodsReturnAcceptanceEndpoints();' in program_text)
add('frontend page exists', page.exists() and 'Goods return / exchange operational acceptance' in page_text and 'goods-return/acceptance' in page_text)
add('menus linked', '/goods-return-acceptance' in shell_text and '/goods-return-acceptance' in legacy_text)
add('access rule linked', '/goods-return-acceptance' in access_text and 'Goods Return Acceptance' in access_text)
add('version updated', all(token in app_info_text for token in ['4.12.57', 'Stage 11D-142 Goods Return Exchange Acceptance', 'GARMETIX-11D142-20260703-4257']) and "APP_VERSION = '4.12.57'" in app_version_text and '<Version>4.12.57</Version>' in csproj_text)
add('docs/release exist', release.exists() and doc_stage.exists() and doc_module.exists())

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(('PASS' if ok else 'FAIL') + ' - ' + name)
if failed:
    print('\nFailed checks:')
    for name in failed:
        print(' - ' + name)
    sys.exit(1)
print('\nStage 11D-142 static validation passed.')

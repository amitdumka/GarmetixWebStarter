#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
checks = []

def add(name, ok):
    checks.append((name, bool(ok)))

endpoint = ROOT / 'backend/Garmetix.Api/Closeout/FinancialYearCloseoutEndpoints.cs'
program = ROOT / 'backend/Garmetix.Api/Program.cs'
page = ROOT / 'frontend/garmetix-web/pages/financial-year-closeout/index.vue'
app_shell = ROOT / 'frontend/garmetix-web/components/AppShell.vue'
legacy_shell = ROOT / 'frontend/garmetix-web/components/AppShellLegacy.vue'
access = ROOT / 'frontend/garmetix-web/composables/useAccessControl.ts'
app_info = ROOT / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs'
app_version = ROOT / 'frontend/garmetix-web/utils/appVersion.ts'
csproj = ROOT / 'backend/Garmetix.Api/Garmetix.Api.csproj'
stage_doc = ROOT / 'docs/stages/stage-11/Stage11D143-Financial-Year-Closeout-Dashboard-v4.12.58.md'
module_doc = ROOT / 'docs/modules/accounting/financial-year-closeout-dashboard-v4.12.58.md'
release = ROOT / 'RELEASE-v4.12.58.txt'

texts = {p: p.read_text(errors='ignore') if p.exists() else '' for p in [endpoint, program, page, app_shell, legacy_shell, access, app_info, app_version, csproj, stage_doc, module_doc, release]}

add('backend endpoint file exists', endpoint.exists())
add('frontend page exists', page.exists())
add('endpoint route registered', 'MapFinancialYearCloseoutEndpoints' in texts[program] and 'using Garmetix.Api.Closeout;' in texts[program])
add('api routes exist', '/api/financial-year-closeout' in texts[endpoint] and '/evidence.csv' in texts[endpoint])
add('closeout checks present', all(token in texts[endpoint] for token in ['SALE_JOURNAL_MISSING', 'PURCHASE_JOURNAL_MISSING', 'NEGATIVE_STOCK', 'FY_LOCK_MISSING', 'PAYROLL_OUTSTANDING']))
add('CSV export implemented', 'BuildCsv' in texts[endpoint] and 'text/csv' in texts[endpoint])
add('frontend route calls API', 'financial-year-closeout' in texts[page] and 'CSV evidence' in texts[page] and 'FY lock evidence' in texts[page])
add('menu links added', '/financial-year-closeout' in texts[app_shell] and '/financial-year-closeout' in texts[legacy_shell])
add('access route added', '/financial-year-closeout' in texts[access])
add('version updated', all(token in texts[app_info] for token in ['4.12.58', 'Stage 11D-143 Financial Year Closeout Dashboard', 'GARMETIX-11D143-20260703-4258']) and "APP_VERSION = '4.12.58'" in texts[app_version] and '<Version>4.12.58</Version>' in texts[csproj])
add('docs added', stage_doc.exists() and module_doc.exists() and release.exists())

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(f"[{'PASS' if ok else 'FAIL'}] {name}")
if failed:
    print('\nFailed checks:')
    for name in failed:
        print(f' - {name}')
    sys.exit(1)
print('\nStage 11D-143 static validation passed.')

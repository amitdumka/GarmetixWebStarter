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

def exists(rel: str) -> bool:
    return (ROOT / rel).exists()

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
print_endpoint = read('backend/Garmetix.Api/Production/PrintAcceptanceEndpoints.cs')
print_page = read('frontend/garmetix-web/pages/print-final-acceptance/index.vue')
store_page = read('frontend/garmetix-web/pages/store-day/index.vue')
app_shell = read('frontend/garmetix-web/components/AppShell.vue')
dashboard_page = read('frontend/garmetix-web/pages/dashboard/index.vue')
dashboard_endpoint = read('backend/Garmetix.Api/Dashboard/DashboardEndpoints.cs')
dashboard_tests = read('backend/Garmetix.Api.Tests/Dashboard/DashboardHomeRoutingTests.cs')
release_checks = read('backend/Garmetix.Api/Release/ReleaseStabilizationEndpoints.cs')
catalog = read('backend/Garmetix.Api/Testing/TestAutomationCatalog.cs')
linux_smoke = read('scripts/linux/smoke-test.sh')
windows_smoke = read('scripts/windows/smoke-test.ps1')
readme = read('README.md')
docs_readme = read('docs/README.md')
roadmap = read('docs/planning/CURRENT-ROADMAP.md')
issues = read('docs/planning/ISSUES.md')

add('frontend package version 4.9.18', pkg.get('version') == '4.9.18')
add('frontend lock version 4.9.18', lock.get('version') == '4.9.18' and lock.get('packages', {}).get('', {}).get('version') == '4.9.18')
add('frontend app version package19', "APP_VERSION = '4.9.18'" in app_version and 'Stage 8I Package 19 Print/PDF Acceptance and Store Operations Landing' in app_version and 'GARMETIX-8I-20260619-49180' in app_version)
add('backend app version package19', 'Version = "4.9.18"' in backend_version and 'Stage 8I Package 19 Print/PDF Acceptance and Store Operations Landing' in backend_version and 'GARMETIX-8I-20260619-49180' in backend_version)
add('backend project version package19', '<Version>4.9.18</Version>' in backend_project and '<AssemblyVersion>4.9.18.0</AssemblyVersion>' in backend_project and '4.9.18-print-pdf-acceptance-store-operations' in backend_project)

for token in ['salesInvoice', 'salesReturn', 'purchaseReturn', 'commercialNote', 'nonGstGoods', 'salaryPayslip', 'salaryPayment']:
    add(f'print acceptance expanded {token}', f'"{token}"' in print_endpoint)
add('print acceptance existing documents retained', all(token in print_endpoint for token in ['"voucher"', '"cashVoucher"', '"pettyCash"', '"purchaseInward"', '"tailoringOrder"', '"gstReturn"']))
add('print page expanded handover copy', 'sales invoices, returns, vouchers, petty cash, purchase, payroll, tailoring, non-GST goods and GST exports' in print_page and 'PDF output' in print_page)
add('print quick navigation expanded', 'to="/billing"' in print_page and 'to="/sales-return"' in print_page and 'to="/purchase-return"' in print_page and 'to="/non-gst-goods"' in print_page and 'to="/payroll"' in print_page)

add('store operations page label', 'title="Store Operations"' in store_page and 'Day opened from Store Operations page' in store_page)
add('store operations shell menu label', "label: 'Store Operations'" in app_shell and "return '/store-day'" in app_shell)
add('store operations access label', "path: '/store-day', label: 'Store Operations'" in access)
add('store operations backend tag', '.WithTags("Store Operations")' in read('backend/Garmetix.Api/StoreDay/StoreDayEndpoints.cs'))
add('dashboard backend store operations landing', '"/store-day"' in dashboard_endpoint and '"StoreOperations"' in dashboard_endpoint and 'biller users start at Store Operations' in dashboard_endpoint)
add('dashboard frontend local landing', "return '/store-day'" in dashboard_page and "role.includes('salesman')" in dashboard_page)
add('dashboard tests store operations', 'ResolveHome_ReturnsStoreOperations_ForSalesman' in dashboard_tests and 'ResolveHome_ReturnsStoreOperations_ForStoreManager' in dashboard_tests and 'Assert.Equal("/store-day", home.Route)' in dashboard_tests)
add('release dashboard contract store operations', 'storeManagerHome.Route == "/store-day"' in release_checks and 'store manager and biller start at Store Operations' in release_checks)

add('manifest print acceptance', 'PRINT_PDF_ACCEPTANCE' in catalog and 'print-pdf-acceptance-drill.sh' in catalog)
add('print drill script exists', exists('scripts/linux/print-pdf-acceptance-drill.sh') and 'print-acceptance/status' in read('scripts/linux/print-pdf-acceptance-drill.sh'))
add('print validation script exists', exists('scripts/validation/print-pdf-acceptance-check.py'))
add('smoke package19 expected version', 'GARMETIX_EXPECTED_VERSION:-4.9.18' in linux_smoke and 'PRINT_PDF_ACCEPTANCE' in linux_smoke and 'ExpectedVersion = "4.9.18"' in windows_smoke and 'PRINT_PDF_ACCEPTANCE' in windows_smoke)
add('operation docs package19', exists('docs/operations/Print-PDF-Acceptance-Store-Operations-v4.9.18.md'))
add('stage notes package19', exists('docs/stages/stage-8/Stage8I-Package19-PrintPdfAcceptance-StoreOperations-v4.9.18-Notes.md'))
add('readme current package19', 'Stage 8I Package 19 Print/PDF Acceptance and Store Operations Landing v4.9.18' in readme and 'GARMETIX-8I-20260619-49180' in readme)
add('docs readme current package19', 'Stage 8I Package 19 Print/PDF Acceptance and Store Operations Landing v4.9.18' in docs_readme and 'GARMETIX-8I-20260619-49180' in docs_readme)
add('roadmap package19', 'Stage 8I Package 19 Print/PDF Acceptance and Store Operations Landing / v4.9.18' in roadmap and 'Store Manager and biller/Salesman users now land first on Store Operations' in roadmap)
add('issues package19', 'Print/PDF final acceptance had incomplete document coverage' in issues and 'Store Manager and biller users landed on a dashboard' in issues)

# Run the focused print/PDF validation as part of the package-level check.
try:
    subprocess.run([sys.executable, str(ROOT / 'scripts/validation/print-pdf-acceptance-check.py')], cwd=ROOT, check=True)
except subprocess.CalledProcessError:
    errors.append('print-pdf-acceptance-check subprocess')

if errors:
    print('Stage 8I Package 19 static checks failed:', file=sys.stderr)
    for error in errors:
        print(f' - {error}', file=sys.stderr)
    if missing_routes:
        print('Missing route rules:', ', '.join(missing_routes), file=sys.stderr)
    sys.exit(1)

print('Stage 8I Package 19 static checks passed.')

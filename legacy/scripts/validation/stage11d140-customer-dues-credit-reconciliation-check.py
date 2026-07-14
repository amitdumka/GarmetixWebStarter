#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
checks: list[tuple[str, bool]] = []

def add(name: str, ok: bool):
    checks.append((name, ok))

backend = ROOT / 'backend/Garmetix.Api/Customers/CustomerDuesReconciliationEndpoints.cs'
program = ROOT / 'backend/Garmetix.Api/Program.cs'
page = ROOT / 'frontend/garmetix-web/pages/customers/dues-reconciliation.vue'
customers = ROOT / 'frontend/garmetix-web/pages/customers/index.vue'
shell = ROOT / 'frontend/garmetix-web/components/AppShell.vue'
legacy_shell = ROOT / 'frontend/garmetix-web/components/AppShellLegacy.vue'
app_info = ROOT / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs'
app_version = ROOT / 'frontend/garmetix-web/utils/appVersion.ts'
csproj = ROOT / 'backend/Garmetix.Api/Garmetix.Api.csproj'

backend_text = backend.read_text(encoding='utf-8') if backend.exists() else ''
program_text = program.read_text(encoding='utf-8') if program.exists() else ''
page_text = page.read_text(encoding='utf-8') if page.exists() else ''
customers_text = customers.read_text(encoding='utf-8') if customers.exists() else ''
shell_text = shell.read_text(encoding='utf-8') if shell.exists() else ''
legacy_text = legacy_shell.read_text(encoding='utf-8') if legacy_shell.exists() else ''
app_info_text = app_info.read_text(encoding='utf-8') if app_info.exists() else ''
app_version_text = app_version.read_text(encoding='utf-8') if app_version.exists() else ''
csproj_text = csproj.read_text(encoding='utf-8') if csproj.exists() else ''

add('backend endpoint file exists', backend.exists())
add('backend route mapped', 'MapCustomerDuesReconciliationEndpoints' in backend_text and 'app.MapCustomerDuesReconciliationEndpoints();' in program_text)
add('backend exposes JSON and CSV endpoints', 'dues-reconciliation' in backend_text and 'evidence.csv' in backend_text and 'BuildCsv' in backend_text)
add('backend checks dues, advances, credit notes and bank mapping', all(token in backend_text for token in ['CustomerAdvanceReceipts', 'CommercialNotes', 'CreditBalance', 'RequiresBankMapping', 'CUSTOMER_CREDIT_BALANCE_MISMATCH']))
add('frontend page exists', page.exists())
add('frontend page calls endpoint and CSV export', 'customers/dues-reconciliation' in page_text and 'CSV evidence' in page_text and 'creditSources' in page_text)
add('CRM nav includes page', '/customers/dues-reconciliation' in shell_text and '/customers/dues-reconciliation' in legacy_text)
add('Customer register has shortcut', "router.push('/customers/dues-reconciliation')" in customers_text)
add('version updated', all(token in app_info_text for token in ['4.12.55', 'Stage 11D-140 Customer Dues Credit Reconciliation', 'GARMETIX-11D140-20260702-4255']) and "APP_VERSION = '4.12.55'" in app_version_text and '<Version>4.12.55</Version>' in csproj_text)
add('docs and release notes exist', (ROOT / 'docs/stages/stage-11/Stage11D140-Customer-Dues-Credit-Reconciliation-v4.12.55.md').exists() and (ROOT / 'docs/modules/crm/customer-dues-credit-reconciliation-v4.12.55.md').exists() and (ROOT / 'RELEASE-v4.12.55.txt').exists())

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(f"{'PASS' if ok else 'FAIL'}: {name}")
if failed:
    raise SystemExit(f"stage11d140 validation failed: {', '.join(failed)}")
print('stage11d140 validation passed')

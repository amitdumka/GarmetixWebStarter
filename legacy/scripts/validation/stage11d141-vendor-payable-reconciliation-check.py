#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
checks = []

def add(name, ok):
    checks.append((name, bool(ok)))

endpoint = ROOT / 'backend/Garmetix.Api/Purchase/VendorPayableReconciliationEndpoints.cs'
program = ROOT / 'backend/Garmetix.Api/Program.cs'
page = ROOT / 'frontend/garmetix-web/pages/purchase/vendor-payable-reconciliation.vue'
shell = ROOT / 'frontend/garmetix-web/components/AppShell.vue'
legacy_shell = ROOT / 'frontend/garmetix-web/components/AppShellLegacy.vue'
app_info = ROOT / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs'
app_version = ROOT / 'frontend/garmetix-web/utils/appVersion.ts'
csproj = ROOT / 'backend/Garmetix.Api/Garmetix.Api.csproj'
release = ROOT / 'RELEASE-v4.12.56.txt'

endpoint_text = endpoint.read_text() if endpoint.exists() else ''
program_text = program.read_text() if program.exists() else ''
page_text = page.read_text() if page.exists() else ''
shell_text = shell.read_text() if shell.exists() else ''
legacy_text = legacy_shell.read_text() if legacy_shell.exists() else ''
app_info_text = app_info.read_text() if app_info.exists() else ''
app_version_text = app_version.read_text() if app_version.exists() else ''
csproj_text = csproj.read_text() if csproj.exists() else ''

add('backend endpoint file exists', endpoint.exists())
add('endpoint route mapped', 'MapVendorPayableReconciliationEndpoints' in endpoint_text and 'purchase/vendor-payable-reconciliation' in endpoint_text and 'evidence.csv' in endpoint_text)
add('program maps endpoint', 'app.MapVendorPayableReconciliationEndpoints();' in program_text)
add('frontend page exists', page.exists() and 'Vendor payable / purchase settlement reconciliation' in page_text and 'CSV evidence' in page_text)
add('sidebar links exist', '/purchase/vendor-payable-reconciliation' in shell_text and '/purchase/vendor-payable-reconciliation' in legacy_text)
add('vendor payments shortcut exists', 'Payable Reco' in (ROOT / 'frontend/garmetix-web/pages/vendor-payments/index.vue').read_text())
add('important checks present', all(token in endpoint_text for token in [
    'VENDOR_MASTER_BILL_MISMATCH',
    'VENDOR_MASTER_PAID_MISMATCH',
    'PURCHASE_INVOICE_STATUS_MISMATCH',
    'VENDOR_PAYMENT_BANK_MAPPING_MISSING',
    'VENDOR_DEBIT_NOTE_OVERADJUSTED',
    'VENDOR_PAYMENT_VOUCHER_AMOUNT_MISMATCH'
]))
add('version updated', all(token in app_info_text for token in ['4.12.56', 'Stage 11D-141 Vendor Payable Reconciliation', 'GARMETIX-11D141-20260702-4256']) and "APP_VERSION = '4.12.56'" in app_version_text and '<Version>4.12.56</Version>' in csproj_text)
add('docs and release notes exist', (ROOT / 'docs/stages/stage-11/Stage11D141-Vendor-Payable-Reconciliation-v4.12.56.md').exists() and (ROOT / 'docs/modules/purchase-inventory/vendor-payable-reconciliation-v4.12.56.md').exists() and release.exists())

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(f"{'PASS' if ok else 'FAIL'} - {name}")

if failed:
    print('\nFailed checks:')
    for name in failed:
        print(f'- {name}')
    sys.exit(1)

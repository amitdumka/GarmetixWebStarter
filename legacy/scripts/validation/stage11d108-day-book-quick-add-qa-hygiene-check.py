#!/usr/bin/env python3
from pathlib import Path
import sys

checks = {
    'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs': ['4.12.23', 'Stage 11D-108 Day Book Quick Add + QA Hygiene', 'GARMETIX-11D108-20260630-4223'],
    'frontend/garmetix-web/utils/appVersion.ts': ["APP_VERSION = '4.12.23'", 'Stage 11D-108 Day Book Quick Add + QA Hygiene', 'GARMETIX-11D108-20260630-4223'],
    'frontend/garmetix-web/package.json': ['"version": "4.12.23"'],
    'backend/Garmetix.Api/Garmetix.Api.csproj': ['<Version>4.12.23</Version>', '4.12.23-stage11d108-day-book-quick-add-qa-hygiene'],
    'frontend/garmetix-web/pages/day-book/index.vue': ['quickAddItems', 'New Sale Invoice', 'New Purchase Inward', 'Add Vendor Payment', 'New Book Voucher', 'New Cash Voucher', 'UDropdownMenu :items="quickAddItems"'],
    'frontend/garmetix-web/pages/vendor-payments/index.vue': ['const route = useRoute()', 'openCreatePaymentFromRoute', "route.query.new", "startCreatePayment('advance')"],
    'frontend/garmetix-web/pages/vouchers/index.vue': ['openCreateVoucherFromRoute', 'route.query.new', 'startCreate()'],
    'frontend/garmetix-web/pages/cash-vouchers/index.vue': ['openCreateCashVoucherFromRoute', 'route.query.new', 'startCreate()'],
    'frontend/garmetix-web/composables/useAccessControl.ts': ["path: '/admin-data'", "path: '/parties'"],
    'frontend/garmetix-web/components/AppShell.vue': ["to: '/parties'"],
    'frontend/garmetix-web/components/AppShellLegacy.vue': ["to: '/parties'", "to: '/admin-data'", "to: '/payroll/finalization'", "to: '/marketing/digital-bills'", "to: '/marketing/campaigns'"],
    'scripts/validation/frontend-route-access-check.py': ['"/i/[token]"'],
    'docs/stages/stage-11/Stage11D108-Day-Book-Quick-Add-QA-Hygiene-v4.12.23.md': ['Day Book Quick Add', 'v4.12.23'],
}

failed = False
for file, tokens in checks.items():
    path = Path(file)
    if not path.exists():
        print(f'MISSING FILE: {file}')
        failed = True
        continue
    text = path.read_text(errors='ignore')
    for token in tokens:
        if token not in text:
            print(f'MISSING TOKEN in {file}: {token}')
            failed = True

if failed:
    print('Stage 11D-108 validation FAILED')
    sys.exit(1)
print('Stage 11D-108 validation passed')

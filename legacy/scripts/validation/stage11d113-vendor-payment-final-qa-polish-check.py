#!/usr/bin/env python3
from pathlib import Path
import sys

checks = {
    'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs': ['4.12.28', 'Stage 11D-113 Vendor Payment Final QA Polish', 'GARMETIX-11D113-20260630-4228', 'vendor payment final QA polish'],
    'frontend/garmetix-web/utils/appVersion.ts': ["APP_VERSION = '4.12.28'", 'Stage 11D-113 Vendor Payment Final QA Polish', 'GARMETIX-11D113-20260630-4228', 'vendor payment final QA polish'],
    'frontend/garmetix-web/package.json': ['"version": "4.12.28"'],
    'backend/Garmetix.Api/Garmetix.Api.csproj': ['<Version>4.12.28</Version>', '4.12.28-stage11d113-vendor-payment-final-qa-polish'],
    'frontend/garmetix-web/pages/vendor-payments/index.vue': [
        'openPayment(row.original)',
        'startEditPayment(row.original)',
        'askDeletePayment(row.original)',
        "api.get<any>(`purchase/payments/${row.id}`)",
        "api.update<any>('purchase/payments'",
        "api.remove('purchase/payments'",
        'route.query.paymentId',
        'route.query.vendorPaymentId',
        'UiConfirmDeleteModal',
        'Vendor Payment Detail'
    ],
    'backend/Garmetix.Api/DayBook/DayBookEndpoints.cs': [
        '/vendor-payments?paymentId=',
        'vendorPaymentVendors',
        'Open Vendor Payment'
    ],
    'docs/stages/stage-11/Stage11D113-Vendor-Payment-Final-QA-Polish-v4.12.28.md': ['Stage 11D-113', 'v4.12.28', 'view', 'edit', 'delete', 'paymentId']
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

page = Path('frontend/garmetix-web/pages/vendor-payments/index.vue').read_text(errors='ignore')
for token in ['View', 'Edit', 'Delete', 'Open Voucher']:
    if token not in page:
        print(f'MISSING vendor payment action label: {token}')
        failed = True

day_book = Path('backend/Garmetix.Api/DayBook/DayBookEndpoints.cs').read_text(errors='ignore')
if '/purchase?vendorPaymentId=' in day_book:
    print('Day Book vendor payment source still opens Purchase page instead of Vendor Payments page')
    failed = True

if failed:
    print('Stage 11D-113 validation FAILED')
    sys.exit(1)
print('Stage 11D-113 validation passed')

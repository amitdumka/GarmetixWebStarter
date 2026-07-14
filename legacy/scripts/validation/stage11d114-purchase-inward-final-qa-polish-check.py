#!/usr/bin/env python3
from pathlib import Path
import sys

checks = {
    'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs': ['4.12.29', 'Stage 11D-114 Purchase Inward Final QA Polish', 'GARMETIX-11D114-20260630-4229', 'Purchase Inward final QA'],
    'frontend/garmetix-web/utils/appVersion.ts': ["APP_VERSION = '4.12.29'", 'Stage 11D-114 Purchase Inward Final QA Polish', 'GARMETIX-11D114-20260630-4229', 'Purchase Inward final QA'],
    'frontend/garmetix-web/package.json': ['"version": "4.12.29"'],
    'backend/Garmetix.Api/Garmetix.Api.csproj': ['<Version>4.12.29</Version>', '4.12.29-stage11d114-purchase-inward-final-qa-polish'],
    'backend/Garmetix.Api/Purchase/PurchaseEndpoints.cs': [
        'string? dateMode = "inward"',
        'var useEntryDate',
        'item.InwardDate >= fromDate',
        'item.OnDate >= fromDate',
        'var orderedQuery = useEntryDate',
        'movement.OnDate = newInwardDate',
        'UpdatePurchaseInvoiceAsync'
    ],
    'frontend/garmetix-web/pages/purchase/index.vue': [
        "const invoiceDateMode = ref('inward')",
        'purchaseDateModeOptions',
        'dateMode: invoiceDateMode.value',
        "header: 'Inward Date'",
        "header: 'Entry Date'",
        'editPurchaseInvoiceId',
        'route.query.inwardId',
        'route.query.invoiceId',
        'dayBookQueryPart',
        'w-[calc(100vw-2rem)] sm:max-w-5xl xl:max-w-7xl',
        'Changing inward date also moves linked purchase stock movement dates'
    ],
    'frontend/garmetix-web/pages/purchase/new.vue': [
        'purchaseReturnUrl',
        "route.query.fromDayBook ? '/purchase?fromDayBook=1' : '/purchase'",
        'await navigateTo(purchaseReturnUrl.value)',
        ':to="purchaseReturnUrl"'
    ],
    'docs/stages/stage-11/Stage11D114-Purchase-Inward-Final-QA-Polish-v4.12.29.md': ['Stage 11D-114', 'v4.12.29', 'inward date', 'Entry Date', 'Back to Day Book']
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

purchase_page = Path('frontend/garmetix-web/pages/purchase/index.vue').read_text(errors='ignore')
if "{ accessorKey: 'onDate', header: 'Date' }" in purchase_page:
    print('Old generic Date column is still present; Purchase Register should show Inward Date and Entry Date separately')
    failed = True

if failed:
    print('Stage 11D-114 validation FAILED')
    sys.exit(1)
print('Stage 11D-114 validation passed')

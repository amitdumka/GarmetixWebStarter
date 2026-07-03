#!/usr/bin/env python3
from pathlib import Path
import sys

checks = {
    'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs': ['4.12.30', 'Stage 11D-115 Sale Invoice Final QA Polish', 'GARMETIX-11D115-20260630-4230', 'Sale Invoice final QA'],
    'frontend/garmetix-web/utils/appVersion.ts': ["APP_VERSION = '4.12.30'", 'Stage 11D-115 Sale Invoice Final QA Polish', 'GARMETIX-11D115-20260630-4230', 'Sale Invoice final QA'],
    'frontend/garmetix-web/package.json': ['"version": "4.12.30"'],
    'backend/Garmetix.Api/Garmetix.Api.csproj': ['<Version>4.12.30</Version>', '4.12.30-stage11d115-sale-invoice-final-qa-polish'],
    'frontend/garmetix-web/pages/billing/index.vue': [
        'garmetix.billing.invoiceRegisterState.v1',
        'restoreBillingState',
        'persistBillingState',
        'focusInvoiceInRegister',
        'sourceWithDayBookReturnHint',
        "route.query.fromDayBook === '1'",
        'receiptSummaryRows',
        'selectedReceiptPaymentRows',
        "content: 'max-w-5xl'",
        'Payment ID',
        'Invoice ID',
        'paymentModeLabel'
    ],
    'frontend/garmetix-web/pages/billing/new.vue': [
        'billingReturnUrl',
        "route.query.fromDayBook ? '/billing?fromDayBook=1' : '/billing'",
        'Open in Register',
        'invoiceId=${lastSavedInvoice.invoiceId}'
    ],
    'backend/Garmetix.Api/Billing/BillingDtos.cs': ['public sealed record ReceiptPaymentDto(', 'Guid Id,', 'DateTime OnDate'],
    'backend/Garmetix.Api/Billing/BillingEndpoints.cs': ['new ReceiptPaymentDto(', 'item.Id,', 'item.OnDate'],
    'backend/Garmetix.Api/Marketing/DigitalBillCrmService.cs': ['new ReceiptPaymentDto(', 'entity.Id,', 'entity.OnDate'],
    'docs/stages/stage-11/Stage11D115-Sale-Invoice-Final-QA-Polish-v4.12.30.md': ['Stage 11D-115', 'v4.12.30', 'Sale Invoice', 'Day Book', 'payment ID']
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

billing_page = Path('frontend/garmetix-web/pages/billing/index.vue').read_text(errors='ignore')
if "title=\"Invoice Receipt\" :ui=\"{ content: 'max-w-3xl' }\"" in billing_page:
    print('Sale receipt modal is still narrow max-w-3xl')
    failed = True
if 'selectedReceipt.payments"' in billing_page:
    print('Receipt still renders raw selectedReceipt.payments instead of formatted payment rows')
    failed = True

if failed:
    print('Stage 11D-115 validation FAILED')
    sys.exit(1)
print('Stage 11D-115 validation passed')

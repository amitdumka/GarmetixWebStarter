#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
checks = {
    'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs': [
        '4.11.60',
        'Stage 11D-45 Sale Payment Split Admin Hard Delete Fix',
        'GARMETIX-11D45-20260626-4560',
    ],
    'frontend/garmetix-web/utils/appVersion.ts': [
        "APP_VERSION = '4.11.60'",
        'Stage 11D-45 Sale Payment Split Admin Hard Delete Fix',
    ],
    'backend/Garmetix.Api/Billing/BillingEndpoints.cs': [
        'MapDelete("/sales/{id:guid}/hard-delete", HardDeleteSaleInvoiceAsync)',
        'Hard delete is allowed only after invoice is cancelled/reversed',
        'BillingAdminHardDelete',
        'InvoicePayments.Where(item => item.InvoiceId == invoice.Id)',
    ],
    'backend/Garmetix.Api/Accounting/PettyCashEndpoints.cs': [
        'paymentsByInvoice',
        'item.PaymentMode != PaymentMode.Cash',
        'Non-cash Sale',
    ],
    'backend/Garmetix.Api/StoreDay/StoreDayEndpoints.cs': [
        'currentInvoiceIds.Contains(item.InvoiceId) && item.PaymentMode != PaymentMode.Cash',
    ],
    'frontend/garmetix-web/pages/billing/index.vue': [
        'Hard Delete',
        'hardDeleteInvoice',
        'confirmInvoiceNumber',
    ],
    'frontend/garmetix-web/pages/billing/new.vue': [
        'receiptPaymentRows',
        'paymentModeFromLabel',
        'Items, bill discount and payment split were copied',
    ],
    'docs/stages/stage-11/Stage11D45-Sale-Payment-Split-Admin-Hard-Delete-Fix-v4.11.60.md': [
        '6000 cash + 1100 UPI',
        'DELETE /api/billing/sales/{id}/hard-delete',
    ],
}

failed = False
for rel, tokens in checks.items():
    path = ROOT / rel
    if not path.exists():
        print(f'MISSING FILE: {rel}')
        failed = True
        continue
    text = path.read_text(errors='ignore')
    for token in tokens:
        if token not in text:
            print(f'MISSING TOKEN in {rel}: {token}')
            failed = True

if failed:
    print('Stage 11D-45 validation FAILED')
    sys.exit(1)
print('Stage 11D-45 validation passed')

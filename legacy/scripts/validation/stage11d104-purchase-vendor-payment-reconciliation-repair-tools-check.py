#!/usr/bin/env python3
from pathlib import Path
import sys
checks = {
    'backend/Garmetix.Api/Purchase/PurchaseEndpoints.cs': [
        'MapGet("/payments/reconciliation", GetPurchasePaymentReconciliationAsync)',
        'MapPost("/payments/reconciliation/repair", RepairPurchasePaymentReconciliationAsync)',
        'GetPurchasePaymentReconciliationAsync',
        'RepairPurchasePaymentReconciliationAsync',
        'ResolvePurchaseInvoiceStatus',
        'VendorPaymentReconciliationRepairResponse',
    ],
    'backend/Garmetix.Api/Purchase/PurchaseDtos.cs': [
        'VendorPaymentReconciliationSummaryDto',
        'VendorPaymentVendorMismatchDto',
        'VendorPaymentInvoiceMismatchDto',
        'VendorPaymentArtifactMismatchDto',
        'VendorPaymentReconciliationDto',
        'VendorPaymentReconciliationRepairResponse',
    ],
    'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs': [
        '4.12.19',
        'Stage 11D-104 Purchase Vendor Payment Reconciliation Repair Tools',
        'GARMETIX-11D104-20260629-4219',
    ],
    'frontend/garmetix-web/utils/appVersion.ts': [
        "APP_VERSION = '4.12.19'",
        'Stage 11D-104 Purchase Vendor Payment Reconciliation Repair Tools',
    ],
    'frontend/garmetix-web/package.json': ['"version": "4.12.19"'],
    'backend/Garmetix.Api/Garmetix.Api.csproj': ['<Version>4.12.19</Version>'],
    'docs/stages/stage-11/Stage11D104-Purchase-Vendor-Payment-Reconciliation-Repair-Tools-v4.12.19.md': [
        'GET  /api/purchase/payments/reconciliation',
        'POST /api/purchase/payments/reconciliation/repair',
    ],
    'scripts/runtime/stage11d104-purchase-vendor-payment-reconciliation-api-check.sh': [
        '/api/purchase/payments/reconciliation?take=20',
    ],
}
failed = False
for file, tokens in checks.items():
    p = Path(file)
    if not p.exists():
        print(f'MISSING FILE: {file}')
        failed = True
        continue
    text = p.read_text(errors='ignore')
    for token in tokens:
        if token not in text:
            print(f'MISSING TOKEN in {file}: {token}')
            failed = True
if failed:
    print('Stage 11D-104 validation FAILED')
    sys.exit(1)
print('Stage 11D-104 validation passed')

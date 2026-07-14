#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
checks = {
    'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs': [
        '4.12.18',
        'Stage 11D-103 Purchase Vendor Payment Reconciliation Hardening',
        'GARMETIX-11D103-20260629-4218',
    ],
    'frontend/garmetix-web/utils/appVersion.ts': [
        "APP_VERSION = '4.12.18'",
        'Stage 11D-103 Purchase Vendor Payment Reconciliation Hardening',
    ],
    'frontend/garmetix-web/package.json': ['"version": "4.12.18"'],
    'backend/Garmetix.Api/Garmetix.Api.csproj': [
        '<Version>4.12.18</Version>',
        '4.12.18-purchase-vendor-payment-reconciliation-hardening',
    ],
    'backend/Garmetix.Api/Purchase/PurchaseEndpoints.cs': [
        'RecalculateVendorAndPurchasePaymentStateAsync',
        'SoftDeleteVoucherBankArtifactsAsync',
        'NormalizeOptional(request.ReferenceNumber)',
        'NormalizeOptional(request.PaymentDetails)',
    ],
    'scripts/runtime/stage11d103-purchase-vendor-payment-reconciliation-check.sh': [
        'stage11d103-purchase-vendor-payment-reconciliation-db-check.sql',
    ],
    'scripts/runtime/stage11d103-purchase-vendor-payment-reconciliation-db-check.sql': [
        'Vendor paid mismatch against active purchase payments',
        'Purchase invoice payment/status mismatch',
    ],
    'docs/stages/stage-11/Stage11D103-Purchase-Vendor-Payment-Reconciliation-Hardening-v4.12.18.md': [
        'Stage 11D-103',
        'Vendor paid total recalculation',
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
    print('Stage 11D-103 validation FAILED')
    sys.exit(1)
print('Stage 11D-103 validation passed')

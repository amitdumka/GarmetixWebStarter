#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
checks = [
    (ROOT / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs', ['4.12.12', 'Stage 11D-97 Vyapar Paid Zero Balance Discount Fix', 'GARMETIX-11D97-20260629-4212']),
    (ROOT / 'frontend/garmetix-web/utils/appVersion.ts', ["APP_VERSION = '4.12.12'", 'Stage 11D-97 Vyapar Paid Zero Balance Discount Fix']),
    (ROOT / 'frontend/garmetix-web/package.json', ['"version": "4.12.12"']),
    (ROOT / 'backend/Garmetix.Api/Garmetix.Api.csproj', ['<Version>4.12.12</Version>', '4.12.12-vyapar-paid-zero-balance-discount-fix']),
    (ROOT / 'backend/Garmetix.Api/SaleImport/VyaparSaleImportService.cs', [
        'IsVyaparPaidWithZeroBalance',
        'ResolveVyaparBillSettlement',
        'ApplyVyaparBillDiscountToInvoiceItems',
        'PaymentStatus is Paid and Balance is zero',
        'BillDiscountAmount = settlement.BillDiscountAmount',
        'ParseDecimal(FirstNonEmpty(record.Get("Balance Due"), record.Get("Balance")))'
    ]),
    (ROOT / 'docs/stages/stage-11/Stage11D97-Vyapar-Paid-Zero-Balance-Discount-Fix-v4.12.12.md', ['Payment Status = Paid', 'Balance', 'bill-level discount']),
]

failed = False
for path, tokens in checks:
    text = path.read_text(errors='ignore') if path.exists() else ''
    if not path.exists():
        print(f'MISSING FILE: {path}')
        failed = True
        continue
    for token in tokens:
        if token not in text:
            print(f'MISSING TOKEN in {path}: {token}')
            failed = True

if failed:
    print('Stage 11D-97 validation FAILED')
    sys.exit(1)
print('Stage 11D-97 validation passed')

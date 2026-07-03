#!/usr/bin/env python3
from pathlib import Path
import sys

checks = {
    'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs': ['4.12.22', 'Stage 11D-107 Day Book Source Deep Links'],
    'frontend/garmetix-web/utils/appVersion.ts': ["APP_VERSION = '4.12.22'", 'Stage 11D-107 Day Book Source Deep Links'],
    'frontend/garmetix-web/package.json': ['"version": "4.12.22"'],
    'backend/Garmetix.Api/Garmetix.Api.csproj': ['<Version>4.12.22</Version>', '4.12.22-stage11d107-day-book-source-deep-links'],
    'frontend/garmetix-web/pages/billing/index.vue': ['const route = useRoute()', 'openBillingDeepLinkFromRoute', 'route.query.invoiceId'],
    'frontend/garmetix-web/pages/purchase/index.vue': ['const route = useRoute()', 'openPurchaseDeepLinkFromRoute', 'route.query.purchaseInvoiceId', 'route.query.vendorPaymentId'],
    'frontend/garmetix-web/pages/vouchers/index.vue': ['const route = useRoute()', 'openVoucherDeepLinkFromRoute', 'route.query.voucherId'],
    'frontend/garmetix-web/pages/cash-vouchers/index.vue': ['const route = useRoute()', 'openCashVoucherDeepLinkFromRoute', 'route.query.cashVoucherId'],
    'docs/stages/stage-11/Stage11D107-Day-Book-Source-Deep-Links-v4.12.22.md': ['Day Book Source Deep Links'],
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
    print('Stage 11D-107 validation FAILED')
    sys.exit(1)
print('Stage 11D-107 validation passed')

#!/usr/bin/env python3
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
checks = [
    ("backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs", [
        "4.12.17",
        "Stage 11D-102 Purchase Vendor Payment Live QA Fixes",
    ]),
    ("frontend/garmetix-web/utils/appVersion.ts", [
        "APP_VERSION = '4.12.17'",
        "Stage 11D-102 Purchase Vendor Payment Live QA Fixes",
    ]),
    ("frontend/garmetix-web/package.json", [
        '"version": "4.12.17"',
    ]),
    ("backend/Garmetix.Api/Garmetix.Api.csproj", [
        "<Version>4.12.17</Version>",
        "4.12.17-purchase-vendor-payment-live-qa-fixes",
    ]),
    ("backend/Garmetix.Api/Purchase/PurchaseEndpoints.cs", [
        'group.MapGet("/payments", SearchPurchasePaymentsAsync)',
        "private static async Task<PagedPurchasePaymentsDto> SearchPurchasePaymentsAsync",
        "includeDeleted = false",
        "CashAmount",
        "NonCashAmount",
    ]),
    ("backend/Garmetix.Api/Purchase/PurchaseDtos.cs", [
        "public sealed record PagedPurchasePaymentsDto",
        "IReadOnlyList<PurchasePaymentRegisterDto> Items",
        "decimal CashAmount",
        "decimal NonCashAmount",
    ]),
    ("frontend/garmetix-web/pages/purchase/index.vue", [
        "paymentSearch",
        "paymentModeFilter",
        "paymentTotalPages",
        "purchase/payments?",
        "Total payments",
        "Bank / UPI / Cheque",
    ]),
    ("docs/stages/stage-11/Stage11D102-Purchase-Vendor-Payment-Live-QA-Fixes-v4.12.17.md", [
        "Stage 11D-102",
        "GET /api/purchase/payments",
        "Vendor Payments panel now has",
    ]),
    ("docs/stages/stage-11/NEXT-PART-AFTER-v4.12.17.md", [
        "Stage 11D-103",
        "Purchase/Vendor Payment Live Error Fixes",
    ]),
    ("scripts/runtime/stage11d102-purchase-vendor-payment-live-qa.sh", [
        "Stage 11D-102 Purchase/Vendor Payment Live QA",
        "4.12.17",
    ]),
]

missing = []
for rel, tokens in checks:
    path = root / rel
    if not path.exists():
        missing.append(f"MISSING FILE: {rel}")
        continue
    text = path.read_text(errors="ignore")
    for token in tokens:
        if token not in text:
            missing.append(f"MISSING TOKEN in {rel}: {token}")

if missing:
    print("Stage 11D-102 validation FAILED")
    for item in missing:
        print(item)
    sys.exit(1)

print("Stage 11D-102 validation passed")

#!/usr/bin/env python3
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
checks = [
    ("backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs", [
        "4.12.16",
        "Stage 11D-101 Purchase Vendor Payment Runtime QA",
        "GARMETIX-11D101-20260629-4216",
        "purchase/vendor payment runtime QA scripts",
    ]),
    ("frontend/garmetix-web/utils/appVersion.ts", [
        "APP_VERSION = '4.12.16'",
        "Stage 11D-101 Purchase Vendor Payment Runtime QA",
        "purchase/vendor payment runtime QA scripts",
    ]),
    ("frontend/garmetix-web/package.json", ['"version": "4.12.16"']),
    ("backend/Garmetix.Api/Garmetix.Api.csproj", [
        "<Version>4.12.16</Version>",
        "4.12.16-purchase-vendor-payment-runtime-qa",
    ]),
    ("scripts/runtime/stage11d101-purchase-vendor-payment-runtime-validation.sh", [
        "EXPECTED_VERSION=\"4.12.16\"",
        "stage11d101-purchase-vendor-payment-db-check.sql",
        "Purchase/Vendor Payment DB checks",
        "Stage 11D-102 Purchase/Vendor Payment Live QA Fixes",
    ]),
    ("scripts/runtime/stage11d101-purchase-vendor-payment-db-check.sql", [
        "PurchaseInvoices.InwardDate",
        "Purchase stock movement date mismatch",
        "Deleted vendor payments that still have active linked voucher",
        "Non-cash active vendor payments without bank account",
    ]),
    ("docs/stages/stage-11/Stage11D101-Purchase-Vendor-Payment-Runtime-QA-v4.12.16.md", [
        "Stage 11D-101",
        "Purchase/Vendor Payment Runtime QA",
        "stage11d101-purchase-vendor-payment-runtime-validation.sh",
        "Stage 11D-102",
    ]),
    ("docs/stages/stage-11/NEXT-PART-AFTER-v4.12.16.md", [
        "Stage 11D-102",
        "Purchase/Vendor Payment Live QA Fixes",
    ]),
]

missing = []
for rel, tokens in checks:
    path = root / rel
    if not path.exists():
        missing.append(f"MISSING FILE: {rel}")
        continue
    text = path.read_text(encoding="utf-8", errors="ignore")
    for token in tokens:
        if token not in text:
            missing.append(f"MISSING TOKEN in {rel}: {token}")

if missing:
    print("Stage 11D-101 validation FAILED")
    for item in missing:
        print(item)
    sys.exit(1)
print("Stage 11D-101 validation passed")

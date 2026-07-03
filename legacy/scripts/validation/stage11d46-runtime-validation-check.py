#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
checks = {
    "backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs": [
        "4.11.61",
        "Stage 11D-46 Runtime Validation Final Bug Fixes",
        "GARMETIX-11D46-20260626-4661",
        "runtime validation runner",
    ],
    "frontend/garmetix-web/utils/appVersion.ts": [
        "APP_VERSION = '4.11.61'",
        "Stage 11D-46 Runtime Validation Final Bug Fixes",
        "GARMETIX-11D46-20260626-4661",
    ],
    "frontend/garmetix-web/package.json": [
        '"version": "4.11.61"',
    ],
    "backend/Garmetix.Api/Garmetix.Api.csproj": [
        "<Version>4.11.61</Version>",
        "4.11.61-stage11d46-runtime-validation-final-bug-fixes",
    ],
    "backend/Garmetix.Api/Accounting/PettyCashEndpoints.cs": [
        "InvoicePayments",
        "PaymentMode.Cash",
    ],
    "backend/Garmetix.Api/StoreDay/StoreDayEndpoints.cs": [
        "InvoicePayments",
        "PaymentMode.Cash",
    ],
    "backend/Garmetix.Api/Billing/BillingEndpoints.cs": [
        "/sales/{id:guid}/hard-delete",
        "BillingAdminHardDelete",
        "InvoicePayments",
    ],
    "frontend/garmetix-web/pages/billing/index.vue": [
        "datePreset",
        "pageSize",
        "hardDelete",
    ],
    "frontend/garmetix-web/pages/billing/new.vue": [
        "originalInvoiceId",
        "replacementApprovalRequested",
        "payments.value = receiptPaymentRows(receipt)",
    ],
    "scripts/runtime/stage11d46-runtime-validation.sh": [
        "docker compose",
        "api/app-info/version",
        "stage11d46-sale-payment-db-check.sql",
        "royalwood-international-v4.11.54",
    ],
    "scripts/runtime/stage11d46-sale-payment-db-check.sql": [
        "SalesInvoices",
        "InvoicePayments",
        "mismatch_count",
        "PaymentMode: 0=Cash, 2=UPI, 12=MixPayments",
    ],
    "docs/stages/stage-11/Stage11D46-Runtime-Validation-Final-Bug-Fixes-v4.11.61.md": [
        "Mixed payment",
        "Invoice replacement approval",
        "Royalwood import",
    ],
}

failed = False
for rel, tokens in checks.items():
    path = ROOT / rel
    if not path.exists():
        print(f"MISSING FILE: {rel}")
        failed = True
        continue
    text = path.read_text(errors="ignore")
    for token in tokens:
        if token not in text:
            print(f"MISSING TOKEN in {rel}: {token}")
            failed = True

# Guard against the old compile failure coming back.
p = ROOT / "backend/Garmetix.Api/InvoiceReplacement/InvoiceReplacementEndpoints.cs"
if p.exists() and "PurchaseInvoice invoice" in p.read_text(errors="ignore"):
    text = p.read_text(errors="ignore")
    bad = "invoice.PaidAmount" in text[text.find("PurchaseInvoice invoice"):]
    if bad:
        print("REGRESSION: InvoiceReplacement purchase snapshot references invoice.PaidAmount")
        failed = True

if failed:
    print("Stage 11D-46 validation FAILED")
    sys.exit(1)
print("Stage 11D-46 validation passed")

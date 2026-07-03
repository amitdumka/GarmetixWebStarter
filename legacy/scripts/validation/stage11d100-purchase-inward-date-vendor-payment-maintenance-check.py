#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]

checks = [
    ("backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs", [
        "4.12.15",
        "Stage 11D-100 Purchase Inward Date Vendor Payment Maintenance",
        "GARMETIX-11D100-20260629-4214",
    ]),
    ("frontend/garmetix-web/utils/appVersion.ts", [
        "APP_VERSION = '4.12.15'",
        "Stage 11D-100 Purchase Inward Date Vendor Payment Maintenance",
        "GARMETIX-11D100-20260629-4214",
    ]),
    ("frontend/garmetix-web/package.json", [
        '"version": "4.12.15"',
    ]),
    ("backend/Garmetix.Api/Garmetix.Api.csproj", [
        "<Version>4.12.15</Version>",
        "4.12.15-purchase-inward-date-vendor-payment-maintenance",
    ]),
    ("backend/Garmetix.Api/Purchase/PurchaseDtos.cs", [
        "DateTime? InwardDate = null",
        "public sealed record UpdatePurchasePaymentRequest",
        "PaymentMode PaymentMode",
        "Guid? BankAccountId",
    ]),
    ("backend/Garmetix.Api/Purchase/PurchaseEndpoints.cs", [
        'group.MapGet("/payments/{id:guid}", GetPurchasePaymentAsync)',
        'group.MapPut("/payments/{id:guid}", UpdatePurchasePaymentAsync)',
        'group.MapDelete("/payments/{id:guid}", DeletePurchasePaymentAsync)',
        "var inwardDate = request.InwardDate?.Date ?? DateTime.Today",
        "InwardDate = inwardDate",
        "OnDate = inwardDate",
        "UpdatePurchasePaymentAsync",
        "DeletePurchasePaymentAsync",
        "BuildPurchasePaymentDtosAsync",
        "SourceType == \"PurchaseInvoice\"",
    ]),
    ("backend/Garmetix.Api/Numbering/DocumentNumberService.cs", [
        "NextPurchaseInwardAsync(Guid companyId, Guid storeGroupId, Guid storeId, DateTime inwardDate",
        '"PurchaseInward", "INW", inwardDate',
    ]),
    ("backend/Garmetix.Api/PurchaseImport/PurchaseInvoiceImportService.cs", [
        "request.InwardDate?.Date ?? request.SupplierInvoiceDate?.Date ?? DateTime.Today",
        "NextPurchaseInwardAsync(request.CompanyId, request.StoreGroupId, request.StoreId, inwardDate",
        "InwardDate = inwardDate",
        "OnDate = inwardDate",
    ]),
    ("frontend/garmetix-web/pages/purchase/new.vue", [
        "inwardDate: todayInputDate()",
        "inwardDate: form.inwardDate",
        "Inward date",
    ]),
    ("frontend/garmetix-web/pages/purchase/index.vue", [
        "const vendorPayments = ref<any[]>([])",
        "paymentEditOpen",
        "paymentViewOpen",
        "paymentDeleteOpen",
        "purchase/payments/recent?take=150",
        "saveVendorPaymentEdit",
        "confirmDeleteVendorPayment",
        "Vendor Payments",
        "purchaseForm.inwardDate",
        "editInvoiceForm.inwardDate",
    ]),
    ("docs/stages/stage-11/Stage11D100-Purchase-Inward-Date-Vendor-Payment-Maintenance-v4.12.15.md", [
        "Stage 11D-100",
        "Vendor payment",
        "Inward date",
    ]),
    ("README.md", [
        "GarmetixWebStarter v4.12.15",
        "Stage 11D-101",
    ]),
]

failed = False
for rel, tokens in checks:
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

# Guard against the duplicate line bug that can break C# publish.
pe = (ROOT / "backend/Garmetix.Api/Purchase/PurchaseEndpoints.cs").read_text(errors="ignore")
if "var chequeLogs = await db.ChequeLogs\n                    var chequeLogs = await db.ChequeLogs" in pe:
    print("DUPLICATE chequeLogs declaration found in PurchaseEndpoints.cs")
    failed = True

if pe.count("private static async Task<IResult> UpdatePurchasePaymentAsync") != 1:
    print("Expected one UpdatePurchasePaymentAsync method")
    failed = True
if pe.count("private static async Task<IResult> DeletePurchasePaymentAsync") != 1:
    print("Expected one DeletePurchasePaymentAsync method")
    failed = True

if failed:
    print("Stage 11D-100 validation FAILED")
    sys.exit(1)

print("Stage 11D-100 validation passed")

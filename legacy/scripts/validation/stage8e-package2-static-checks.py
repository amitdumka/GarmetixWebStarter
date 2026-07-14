from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = [
    ("backend/Garmetix.Api/Accounting/AccountingPostingService.cs", "public sealed record SalesInvoicePaymentPosting"),
    ("backend/Garmetix.Api/Accounting/AccountingPostingService.cs", "IReadOnlyList<SalesInvoicePaymentPosting>? payments"),
    ("backend/Garmetix.Api/Accounting/AccountingPostingService.cs", "BuildSalesInvoicePaymentSourceReference"),
    ("backend/Garmetix.Api/Accounting/AccountingPostingService.cs", "ResolveSalesInvoiceSettlementLedgerAsync"),
    ("backend/Garmetix.Api/Billing/BillingEndpoints.cs", "Reference.StartsWith($\"SI-{invoice.InvoiceNumber}-PAY-\")"),
    ("backend/Garmetix.Api/Billing/BillingEndpoints.cs", "ToAccountingPaymentPostings(originalPaymentRows)"),
    ("backend/Garmetix.Api/Accounting/AccountingPostingService.cs", "NormalizeSalesInvoiceCancellationPaymentPostings"),
    ("backend/Garmetix.Api/Accounting/AccountingPostingService.cs", "!BankPaymentModes.Contains(paymentMode)"),
    ("backend/Garmetix.Api/Billing/BillingEndpoints.cs", "ToAccountingPaymentPostings(paymentDetails)"),
    ("backend/Garmetix.Api/Billing/BillingEndpoints.cs", "ValidateSingleUseAdjustmentRows"),
    ("backend/Garmetix.Api/Billing/BillingEndpoints.cs", "BuildPaymentDetailsJson"),
    ("backend/Garmetix.Api/Billing/BillingEndpoints.cs", "PaymentDetailsJson = payment.PaymentDetailsJson"),
    ("backend/Garmetix.Api/Billing/BillingDtos.cs", "string? CardLastFour = null"),
    ("backend/Garmetix.Domain/Generated/Models/Inventory/Invoicing.cs", "public string? PaymentDetailsJson"),
    ("backend/Garmetix.Api/Database/DatabaseSchemaRepairService.cs", '"PaymentDetailsJson" text NULL'),
    ("backend/Garmetix.Infrastructure/Data/Migrations/20260616133000_AddInvoicePaymentDetails.cs", "AddInvoicePaymentDetails"),
    ("backend/Garmetix.Api/ImportExport/ImportExportEndpoints.cs", "new List<SalesInvoicePaymentPosting>()"),
    ("frontend/garmetix-web/nuxt.config.ts", "provider: 'local'"),
    ("frontend/garmetix-web/nuxt.config.ts", "google: false"),
    ("frontend/garmetix-web/utils/appVersion.ts", "4.3.8"),
    ("backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs", "GARMETIX-8E-20260616-4380"),
    ("docs/planning/CURRENT-ROADMAP.md", "Stage 8E Package 2"),
]

missing = []
for relative, marker in checks:
    path = root / relative
    if not path.exists():
        missing.append(f"missing file: {relative}")
        continue
    text = path.read_text(encoding="utf-8")
    if marker not in text:
        missing.append(f"missing marker in {relative}: {marker}")

if missing:
    print("Stage 8E Package 2 static validation failed:")
    for item in missing:
        print(f" - {item}")
    raise SystemExit(1)

print("Stage 8E Package 2 static validation passed.")

from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = [
    ("backend/Garmetix.Api/Billing/BillingEndpoints.cs", "ResolveRequiredSalesmanIdAsync"),
    ("backend/Garmetix.Api/Billing/BillingEndpoints.cs", "ResolveExistingOrFallbackSalesmanIdAsync"),
    ("backend/Garmetix.Api/Billing/BillingEndpoints.cs", "Create or activate at least one salesman for this billing store before saving invoices."),
    ("backend/Garmetix.Api/Billing/BillingEndpoints.cs", "SalemanId = salesmanId.Value"),
    ("backend/Garmetix.Api/Billing/BillingEndpoints.cs", "SalemanId = returnSalesmanId.Value"),
    ("backend/Garmetix.Api/Billing/BillingEndpoints.cs", "SalemanId = exchangeSalesmanId.Value"),
    ("backend/Garmetix.Api/Billing/BillingEndpoints.cs", "Original invoice salesman is missing and no active fallback salesman exists for this store."),
    ("backend/Garmetix.Api/ImportExport/ImportExportEndpoints.cs", "using System.Text.Json;"),
    ("backend/Garmetix.Api/ImportExport/ImportExportEndpoints.cs", "defaultSalesmenByStore"),
    ("backend/Garmetix.Api/ImportExport/ImportExportEndpoints.cs", "SalemanId = salesmanId"),
    ("backend/Garmetix.Api/ImportExport/ImportExportEndpoints.cs", "JsonSerializer.Serialize(new Dictionary<string, object?>"),
    ("frontend/garmetix-web/utils/appVersion.ts", "4.3.8"),
    ("backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs", "GARMETIX-8E-20260616-4380"),
    ("backend/Garmetix.Api/Garmetix.Api.csproj", "4.3.8-stage8e-package2-hotfix1"),
    ("docs/planning/CURRENT-ROADMAP.md", "Stage 8E Package 2 Hotfix 1 / v4.3.8"),
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

billing_text = (root / "backend/Garmetix.Api/Billing/BillingEndpoints.cs").read_text(encoding="utf-8")
for banned in ["SalemanId = request.SalesmanId ?? Guid.Empty", "SalemanId = Guid.Empty"]:
    if banned in billing_text:
        missing.append(f"banned blank salesman assignment remains: {banned}")

import_text = (root / "backend/Garmetix.Api/ImportExport/ImportExportEndpoints.cs").read_text(encoding="utf-8")
if '$\"{{\"paymentMode\"' in import_text or '$"{{"paymentMode"' in import_text:
    missing.append("unsafe billing import payment-details interpolated JSON remains")

if missing:
    print("Stage 8E Package 2 Hotfix 1 static validation failed:")
    for item in missing:
        print(f" - {item}")
    raise SystemExit(1)

print("Stage 8E Package 2 Hotfix 1 static validation passed.")

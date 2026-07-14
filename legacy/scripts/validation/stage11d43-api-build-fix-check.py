#!/usr/bin/env python3
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
errors = []

endpoint = root / "backend/Garmetix.Api/InvoiceReplacement/InvoiceReplacementEndpoints.cs"
text = endpoint.read_text(encoding="utf-8")

if "private static object Snapshot(PurchaseInvoice invoice)" not in text:
    errors.append("Purchase invoice snapshot method missing")

purchase_snapshot = text.split("private static object Snapshot(PurchaseInvoice invoice)", 1)[1]
if "invoice.PaidAmount" in purchase_snapshot:
    errors.append("Purchase invoice snapshot still references missing PurchaseInvoice.PaidAmount")

if 'revised.CustomerName ?? "Customer"' not in text:
    errors.append("Pending sale replacement party name fallback missing")

version_files = [
    root / "backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs",
    root / "frontend/garmetix-web/utils/appVersion.ts",
    root / "frontend/garmetix-web/package.json",
    root / "backend/Garmetix.Api/Garmetix.Api.csproj",
    root / "README.md",
]
for path in version_files:
    body = path.read_text(encoding="utf-8")
    if "4.11.58" not in body:
        errors.append(f"Version 4.11.58 missing from {path.relative_to(root)}")

if "Stage 11D-43 API Build Fix" not in (root / "backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs").read_text(encoding="utf-8"):
    errors.append("AppInfo stage label not updated")

if errors:
    print("Stage 11D-43 validation failed:")
    for err in errors:
        print(f"- {err}")
    sys.exit(1)

print("Stage 11D-43 validation passed")

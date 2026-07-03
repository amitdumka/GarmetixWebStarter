#!/usr/bin/env python3
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
checks = [
    ("backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs", ["4.12.06", "Stage 11D-91 Vyapar Sale Import Runtime Validation"]),
    ("frontend/garmetix-web/utils/appVersion.ts", ["APP_VERSION = '4.12.06'", "Stage 11D-91 Vyapar Sale Import Runtime Validation"]),
    ("frontend/garmetix-web/package.json", ['"version": "4.12.06"']),
    ("backend/Garmetix.Api/Garmetix.Api.csproj", ["<Version>4.12.06</Version>", "4.12.06-vyapar-sale-import-runtime-validation"]),
    ("scripts/runtime/stage11d91-vyapar-sale-import-runtime-validation.sh", ["stage11d91-vyapar-sale-import-db-check.sql", "Stage 11D-92 Vyapar Import Batch Undo"]),
    ("scripts/runtime/stage11d91-vyapar-sale-import-db-check.sql", ["SalesInvoices", "VyaparSaleImport", "PaymentDetailsJson", "StockMovements"]),
    ("scripts/runtime/stage11d91-vyapar-preview-curl-template.sh", ["sale-import/vyapar/preview", "GARMETIX_TOKEN"]),
    ("docs/stages/stage-11/Stage11D91-Vyapar-Sale-Import-Runtime-Validation-v4.12.06.md", ["Stage 11D-91", "Stage 11D-92"]),
    ("docs/stages/stage-11/NEXT-PART-AFTER-v4.12.06.md", ["Stage 11D-92", "Bulk Barcode Mapping Upload"]),
]

failed = False
for rel, tokens in checks:
    path = root / rel
    if not path.exists():
        print(f"MISSING FILE: {rel}")
        failed = True
        continue
    text = path.read_text(errors="replace")
    for token in tokens:
        if token not in text:
            print(f"MISSING TOKEN in {rel}: {token}")
            failed = True

old_tokens = ["4.12.05", "Stage 11D-90 Vyapar Sale Import Review Enhancements"]
version_files = [
    root / "backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs",
    root / "frontend/garmetix-web/utils/appVersion.ts",
    root / "frontend/garmetix-web/package.json",
    root / "backend/Garmetix.Api/Garmetix.Api.csproj",
]
for path in version_files:
    text = path.read_text(errors="replace")
    for token in old_tokens:
        if token in text:
            print(f"OLD VERSION TOKEN remains in {path.relative_to(root)}: {token}")
            failed = True

if failed:
    print("Stage 11D-91 validation FAILED")
    sys.exit(1)
print("Stage 11D-91 validation passed")

import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = []


def check(name: str, condition: bool):
    checks.append((name, condition))


billing = (root / "frontend/garmetix-web/pages/billing/index.vue").read_text(encoding="utf-8")
sales_return = (root / "frontend/garmetix-web/pages/sales-return/index.vue").read_text(encoding="utf-8")
audit_progress = (root / "frontend/garmetix-web/composables/useUiAuditProgress.ts").read_text(encoding="utf-8")
frontend_version = (root / "frontend/garmetix-web/utils/appVersion.ts").read_text(encoding="utf-8")
backend_version = (root / "backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs").read_text(encoding="utf-8")
project = (root / "backend/Garmetix.Api/Garmetix.Api.csproj").read_text(encoding="utf-8")
package = json.loads((root / "frontend/garmetix-web/package.json").read_text(encoding="utf-8"))
package_lock = json.loads((root / "frontend/garmetix-web/package-lock.json").read_text(encoding="utf-8"))

check("billing uses shared register", "UiRegisterPanel" in billing)
check("billing retains sanitized retryable errors", all(value in billing for value in ("loadError", "feedback.cleanMessage", '@retry="refresh"')))
check("billing supports invoice search and status filter", all(value in billing for value in ("invoiceStatusFilter", "All statuses", "UiCrudToolbar")))
check("billing invoice workspace remains extra wide", "sm:max-w-6xl xl:max-w-7xl" in billing)
check("billing split payments remain available", all(value in billing for value in ("Split payment rows", "salePayments", "Add Payment Row")))
check("billing print and PDF formats remain available", all(value in billing for value in ("thermal-2", "thermal-3", "downloadInvoicePdf", "printReceipt")))
check("sales return uses shared register and table", "UiRegisterPanel" in sales_return and "UTable" in sales_return)
check("sales return retains sanitized retryable errors", all(value in sales_return for value in ("loadError", "feedback.cleanMessage", '@retry="refresh"')))
check("sales return workspace is wide", "sm:max-w-5xl lg:max-w-6xl" in sales_return)
check("sales return supports refund payment modes", "paymentModeOptions" in sales_return and "refundPaymentMode" in sales_return)
check("non-cash refund requires bank account", all(value in sales_return for value in ("refundRequiresBank", "bankAccountId", "A bank account is required for non-cash refunds.")))
check("sales return preserves item-wise quantities", all(value in sales_return for value in ("returnLines", "invoiceItemId", "clampReturnQuantity")))
check("both pages refresh on workspace change", billing.count('@workspace-change="refresh"') == 1 and sales_return.count('@workspace-change="refresh"') == 1)
check("audit includes package routes", all(route in audit_progress for route in ("'/billing'", "'/sales-return'")))
check("audit storage migrates from v4.0.2", "garmetix.ui-audit.v4.0.3" in audit_progress and "garmetix.ui-audit.v4.0.2" in audit_progress)

frontend_match = re.search(r"APP_VERSION = '([^']+)'", frontend_version)
backend_match = re.search(r'const string Version = "([^"]+)"', backend_version)
project_match = re.search(r"<Version>([^<]+)</Version>", project)
assembly_match = re.search(r"<AssemblyVersion>([^<]+)</AssemblyVersion>", project)
file_match = re.search(r"<FileVersion>([^<]+)</FileVersion>", project)
versions = {
    frontend_match.group(1) if frontend_match else "",
    backend_match.group(1) if backend_match else "",
    project_match.group(1) if project_match else "",
    package.get("version", ""),
    package_lock.get("packages", {}).get("", {}).get("version", "")
}
check("runtime versions remain synchronized after v4.0.3", len(versions) == 1 and next(iter(versions)) >= "4.0.3")
check("assembly and file versions remain synchronized", assembly_match and file_match and assembly_match.group(1) == file_match.group(1))
check("frontend and backend use matching Stage 8 build codes", re.search(r"APP_BUILD_CODE = '([^']+)'", frontend_version).group(1) == re.search(r'const string BuildCode = "([^"]+)"', backend_version).group(1) and "GARMETIX-8" in frontend_version)

failed = [name for name, passed in checks if not passed]
for name, passed in checks:
    print(f"[{'PASS' if passed else 'FAIL'}] {name}")

if failed:
    raise SystemExit(f"{len(failed)} Stage 8A Package 5 check(s) failed")

print(f"Stage 8A Package 5 static validation passed: {len(checks)} checks")

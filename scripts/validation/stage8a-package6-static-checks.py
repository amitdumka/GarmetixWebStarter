import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = []


def check(name: str, condition: bool):
    checks.append((name, condition))


petty_cash = (root / "frontend/garmetix-web/pages/petty-cash/index.vue").read_text(encoding="utf-8")
purchase = (root / "frontend/garmetix-web/pages/purchase/index.vue").read_text(encoding="utf-8")
purchase_return = (root / "frontend/garmetix-web/pages/purchase-return/index.vue").read_text(encoding="utf-8")
audit_progress = (root / "frontend/garmetix-web/composables/useUiAuditProgress.ts").read_text(encoding="utf-8")
frontend_version = (root / "frontend/garmetix-web/utils/appVersion.ts").read_text(encoding="utf-8")
backend_version = (root / "backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs").read_text(encoding="utf-8")
project = (root / "backend/Garmetix.Api/Garmetix.Api.csproj").read_text(encoding="utf-8")
package = json.loads((root / "frontend/garmetix-web/package.json").read_text(encoding="utf-8"))
package_lock = json.loads((root / "frontend/garmetix-web/package-lock.json").read_text(encoding="utf-8"))

check("petty cash payload preserves selected local date", 'onDate: `${form.onDate}T00:00:00`' in petty_cash)
check("petty cash no longer serializes selected date through UTC", 'new Date(`${form.onDate}T00:00:00`).toISOString()' not in petty_cash)
check("petty cash default date is local", "onDate: localDateInput()" in petty_cash and "function localDateInput" in petty_cash)
check("petty cash carry-forward preparation remains intact", "petty-cash-sheets/prepare" in petty_cash and "openingBalance: Number(result.openingBalance || 0)" in petty_cash)
check("purchase uses shared register", "UiRegisterPanel" in purchase)
check("purchase retains sanitized retryable errors", all(value in purchase for value in ("loadError", "feedback.cleanMessage", '@retry="refresh"')))
check("purchase supports status filter and search", all(value in purchase for value in ("invoiceStatusFilter", "All statuses", "UiCrudToolbar")))
check("purchase inward workspace remains extra wide", "sm:max-w-6xl xl:max-w-7xl" in purchase)
check("purchase default dates use local calendar", "return date.toISOString().slice(0, 10)" not in purchase and "date.getFullYear()" in purchase)
check("purchase return uses shared register and table", "UiRegisterPanel" in purchase_return and "UTable" in purchase_return)
check("purchase return retains sanitized retryable errors", all(value in purchase_return for value in ("loadError", "feedback.cleanMessage", '@retry="refresh"')))
check("purchase return workspace is extra wide", "sm:max-w-6xl xl:max-w-7xl" in purchase_return)
check("purchase return date preserves local calendar", 'returnDate: returnDate.value ? `${returnDate.value}T00:00:00` : null' in purchase_return)
check("purchase return no longer converts date through UTC", "new Date(returnDate.value).toISOString()" not in purchase_return)
check("both pages refresh on workspace change", purchase.count('@workspace-change="refresh"') == 1 and purchase_return.count('@workspace-change="refresh"') == 1)
check("audit includes package routes", all(route in audit_progress for route in ("'/purchase'", "'/purchase-return'")))
check("audit storage migrates from v4.0.3", "garmetix.ui-audit.v4.0.4" in audit_progress and "garmetix.ui-audit.v4.0.3" in audit_progress)

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
check("all runtime versions are 4.0.4", versions == {"4.0.4"})
check("assembly and file versions are 4.0.4.0", assembly_match and file_match and assembly_match.group(1) == file_match.group(1) == "4.0.4.0")
check("frontend and backend build codes match", "GARMETIX-8A-20260613-4004" in frontend_version and "GARMETIX-8A-20260613-4004" in backend_version)

failed = [name for name, passed in checks if not passed]
for name, passed in checks:
    print(f"[{'PASS' if passed else 'FAIL'}] {name}")

if failed:
    raise SystemExit(f"{len(failed)} Stage 8A Package 6 check(s) failed")

print(f"Stage 8A Package 6 static validation passed: {len(checks)} checks")

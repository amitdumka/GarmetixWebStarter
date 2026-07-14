import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = []


def check(name: str, condition: bool):
    checks.append((name, condition))


loyalty = (root / "frontend/garmetix-web/pages/loyalty/index.vue").read_text(encoding="utf-8")
petty_cash = (root / "frontend/garmetix-web/pages/petty-cash/index.vue").read_text(encoding="utf-8")
audit_progress = (root / "frontend/garmetix-web/composables/useUiAuditProgress.ts").read_text(encoding="utf-8")
frontend_version = (root / "frontend/garmetix-web/utils/appVersion.ts").read_text(encoding="utf-8")
backend_version = (root / "backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs").read_text(encoding="utf-8")
project = (root / "backend/Garmetix.Api/Garmetix.Api.csproj").read_text(encoding="utf-8")
package = json.loads((root / "frontend/garmetix-web/package.json").read_text(encoding="utf-8"))
package_lock = json.loads((root / "frontend/garmetix-web/package-lock.json").read_text(encoding="utf-8"))

check("loyalty uses shared ledger register", "UiRegisterPanel" in loyalty and "filteredLedger" in loyalty)
check("loyalty has setup and ledger retry states", all(value in loyalty for value in ("loadError", "ledgerError", '@retry="loadCustomerLoyalty(false)"')))
check("loyalty ledger supports search", 'search-placeholder="Search loyalty ledger"' in loyalty)
check("loyalty customer select uses a placeholder without an empty option", 'placeholder="Select customer"' in loyalty and "{ value: '', label: 'Select customer' }" not in loyalty)
check("loyalty summary avoids nested cards", "loyalty-summary-grid" in loyalty and "<UCard><p>Points" not in loyalty)
check("petty cash uses shared register", "UiRegisterPanel" in petty_cash)
check("petty cash retains sanitized retryable errors", "loadError" in petty_cash and "feedback.cleanMessage" in petty_cash and '@retry="refresh"' in petty_cash)
check("petty cash entry uses wide workspace", "sm:max-w-4xl lg:max-w-5xl" in petty_cash)
check("petty cash calculation remains automatic", all(value in petty_cash for value in ("const calculatedCash", "watch(calculatedCash", "form.cashInHand = roundMoney(value)")))
check("petty cash preparation remains transaction based", "petty-cash-sheets/prepare" in petty_cash and "Pre-calculated from transactions" in petty_cash)
check("petty cash A5 color print remains intact", all(value in petty_cash for value in ("@page { size: A5 landscape", "print-color-adjust: exact", "buildPettyCashPrintHtml")))
check("audit includes package routes", all(route in audit_progress for route in ("'/loyalty'", "'/petty-cash'")))
check("audit storage migrates from v4.0.1", "garmetix.ui-audit.v4.0.2" in audit_progress and "garmetix.ui-audit.v4.0.1" in audit_progress)

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
check("runtime versions remain synchronized after v4.0.2", len(versions) == 1 and next(iter(versions)) >= "4.0.2")
check("assembly and file versions remain synchronized", assembly_match and file_match and assembly_match.group(1) == file_match.group(1))
check("frontend and backend use matching Stage 8 build codes", re.search(r"APP_BUILD_CODE = '([^']+)'", frontend_version).group(1) == re.search(r'const string BuildCode = "([^"]+)"', backend_version).group(1) and "GARMETIX-8" in frontend_version)

failed = [name for name, passed in checks if not passed]
for name, passed in checks:
    print(f"[{'PASS' if passed else 'FAIL'}] {name}")

if failed:
    raise SystemExit(f"{len(failed)} Stage 8A Package 4 check(s) failed")

print(f"Stage 8A Package 4 static validation passed: {len(checks)} checks")

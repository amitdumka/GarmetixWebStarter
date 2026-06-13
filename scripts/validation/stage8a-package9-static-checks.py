import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = []


def read(path: str) -> str:
    return (root / path).read_text(encoding="utf-8")


def check(name: str, condition: bool):
    checks.append((name, condition))


dtos = read("backend/Garmetix.Api/Accounting/AccountingDtos.cs")
endpoints = read("backend/Garmetix.Api/Accounting/AccountingEndpoints.cs")
service = read("backend/Garmetix.Api/Accounting/AccountingPostingService.cs")
accounting = read("frontend/garmetix-web/pages/accounting/index.vue")
hr = read("frontend/garmetix-web/pages/hr/index.vue")
audit_progress = read("frontend/garmetix-web/composables/useUiAuditProgress.ts")
frontend_version = read("frontend/garmetix-web/utils/appVersion.ts")
backend_version = read("backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs")
project = read("backend/Garmetix.Api/Garmetix.Api.csproj")
package = json.loads(read("frontend/garmetix-web/package.json"))
package_lock = json.loads(read("frontend/garmetix-web/package-lock.json"))

check("party uses a dedicated save request", "PartySaveRequest" in dtos and "Party party" not in endpoints)
check("bank account uses a dedicated save request", "BankAccountSaveRequest" in dtos and "BankAccount account" not in endpoints)
check("party ledger remains server owned", "EnsurePartyLedgerAsync(party" in service and "party.LedgerId =" in service)
check("bank account ledger remains server owned", "EnsureBankAccountLedgerAsync(account" in service and "account.LedgerId =" in service)
check("accounting payload excludes party and bank ledger ids", "ledgerId: nullableGuid(partyForm.ledgerId)" not in accounting and "ledgerId: nullableGuid(bankAccountForm.ledgerId)" not in accounting)
check("bank transaction party selector is hidden", 'UFormField label="Party"' not in accounting and "partyId: null" in accounting)
check("accounting uses shared retryable register", "UiRegisterPanel" in accounting and ':error="loadError"' in accounting and '@retry="refresh"' in accounting)
check("accounting forms use wide modal workspaces", 'layout="modal"' in accounting and "sm:max-w-5xl lg:max-w-6xl" in accounting)
check("accounting dates preserve local calendar values", "function toApiDate" in accounting and "return `${value || localDateInput()}T00:00:00`" in accounting and "T00:00:00`).toISOString()" not in accounting)
check("HR uses shared retryable register", "UiRegisterPanel" in hr and ':error="loadError"' in hr and '@retry="refresh"' in hr)
check("HR dates preserve local calendar values", "return `${value}T00:00:00`" in hr and "T00:00:00`).toISOString()" not in hr)
check("HR all-store option avoids empty SelectItem values", "ALL_STORES_VALUE" in hr and "{ value: '', label: 'All stores' }" not in hr)
check("audit storage migrates from v4.0.6", "garmetix.ui-audit.v4.0.7" in audit_progress and "garmetix.ui-audit.v4.0.6" in audit_progress)
check("Accounting and HR are reviewed routes", "'/accounting'" in audit_progress and "'/hr'" in audit_progress)

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
    package_lock.get("packages", {}).get("", {}).get("version", ""),
}
check("runtime versions remain synchronized after v4.0.7", len(versions) == 1 and next(iter(versions)) >= "4.0.7")
check("assembly and file versions remain synchronized", assembly_match and file_match and assembly_match.group(1) == file_match.group(1))
check("frontend and backend use a Stage 8A build code", "GARMETIX-8A-" in frontend_version and "GARMETIX-8A-" in backend_version)

failed = [name for name, passed in checks if not passed]
for name, passed in checks:
    print(f"[{'PASS' if passed else 'FAIL'}] {name}")

if failed:
    raise SystemExit(f"{len(failed)} Stage 8A Package 9 check(s) failed")

print(f"Stage 8A Package 9 static validation passed: {len(checks)} checks")

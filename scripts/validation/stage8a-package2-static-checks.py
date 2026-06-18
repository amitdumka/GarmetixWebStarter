import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = []


def check(name: str, condition: bool):
    checks.append((name, condition))


commercial = (root / "frontend/garmetix-web/pages/commercial-notes/index.vue").read_text(encoding="utf-8")
customers = (root / "frontend/garmetix-web/pages/customers/index.vue").read_text(encoding="utf-8")
audit_progress = (root / "frontend/garmetix-web/composables/useUiAuditProgress.ts").read_text(encoding="utf-8")
frontend_version = (root / "frontend/garmetix-web/utils/appVersion.ts").read_text(encoding="utf-8")
backend_version = (root / "backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs").read_text(encoding="utf-8")
project = (root / "backend/Garmetix.Api/Garmetix.Api.csproj").read_text(encoding="utf-8")
package = json.loads((root / "frontend/garmetix-web/package.json").read_text(encoding="utf-8"))
package_lock = json.loads((root / "frontend/garmetix-web/package-lock.json").read_text(encoding="utf-8"))

check("commercial notes use shared register", "UiRegisterPanel" in commercial)
check("commercial notes support search and type filter", "filteredNotes" in commercial and "noteTypeFilter" in commercial)
check("commercial notes show all primary actions", all(label in commercial for label in ("New Credit Note", "New Debit Note", "Customers / Advances")))
check("customers use shared register", customers.count("UiRegisterPanel") >= 2)
check("customers expose loading, error and empty states", all(value in customers for value in ("loadError", "ledgerError", ":empty=")))
check("customers show new customer action", 'label="New Customer"' in customers)
check("customer loyalty ledger can close and retry", "closeLoyalty" in customers and '@retry="openLoyalty(selectedCustomer)"' in customers)
check("new package routes are reviewed", "'/commercial-notes'" in audit_progress and "'/customers'" in audit_progress)

frontend_match = re.search(r"APP_VERSION = '([^']+)'", frontend_version)
backend_match = re.search(r'const string Version = "([^"]+)"', backend_version)
project_match = re.search(r"<Version>([^<]+)</Version>", project)
versions = {
    frontend_match.group(1) if frontend_match else "",
    backend_match.group(1) if backend_match else "",
    project_match.group(1) if project_match else "",
    package.get("version", ""),
    package_lock.get("packages", {}).get("", {}).get("version", "")
}
check("runtime versions remain synchronized in v4", len(versions) == 1 and next(iter(versions)).startswith("4."))
frontend_build = re.search(r"APP_BUILD_CODE = '([^']+)'", frontend_version)
backend_build = re.search(r'const string BuildCode = "([^"]+)"', backend_version)
check(
    "frontend and backend use the same Stage 8 build code",
    frontend_build is not None
    and backend_build is not None
    and frontend_build.group(1) == backend_build.group(1)
    and frontend_build.group(1).startswith("GARMETIX-8")
)

failed = [name for name, passed in checks if not passed]
for name, passed in checks:
    print(f"[{'PASS' if passed else 'FAIL'}] {name}")

if failed:
    raise SystemExit(f"{len(failed)} Stage 8A Package 2 check(s) failed")

print(f"Stage 8A Package 2 static validation passed: {len(checks)} checks")

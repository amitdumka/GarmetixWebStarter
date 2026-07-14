import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = []


def check(name: str, condition: bool):
    checks.append((name, condition))


parties = (root / "frontend/garmetix-web/pages/parties/index.vue").read_text(encoding="utf-8")
vouchers = (root / "frontend/garmetix-web/pages/vouchers/index.vue").read_text(encoding="utf-8")
audit_progress = (root / "frontend/garmetix-web/composables/useUiAuditProgress.ts").read_text(encoding="utf-8")
frontend_version = (root / "frontend/garmetix-web/utils/appVersion.ts").read_text(encoding="utf-8")
backend_version = (root / "backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs").read_text(encoding="utf-8")
project = (root / "backend/Garmetix.Api/Garmetix.Api.csproj").read_text(encoding="utf-8")
package = json.loads((root / "frontend/garmetix-web/package.json").read_text(encoding="utf-8"))
package_lock = json.loads((root / "frontend/garmetix-web/package-lock.json").read_text(encoding="utf-8"))

check("parties use shared register", "UiRegisterPanel" in parties)
check("parties retain sanitized load errors", "loadError" in parties and "feedback.cleanMessage" in parties)
check("parties show customer and vendor actions", all(label in parties for label in ('label="New Customer"', 'label="New Vendor"')))
check("parties provide type, search and retry controls", all(value in parties for value in ('aria-label="Filter party type"', "UiCrudToolbar", '@retry="refresh"')))
check("vouchers use shared register", "UiRegisterPanel" in vouchers)
check("vouchers retain sanitized load errors", "loadError" in vouchers and "feedback.cleanMessage" in vouchers)
check("vouchers support type filter and search", "voucherTypeFilter" in vouchers and "All vouchers" in vouchers and "UiCrudToolbar" in vouchers)
check("voucher create action remains visible", 'label="New Voucher"' in vouchers)
check("audit includes package routes", all(route in audit_progress for route in ("'/parties'", "'/vouchers'")))
check("audit storage migrates from v4.0.0", "garmetix.ui-audit.v4.0.1" in audit_progress and "garmetix.ui-audit.v4.0.0" in audit_progress)

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
check("runtime versions remain synchronized after v4.0.1", len(versions) == 1 and next(iter(versions)) >= "4.0.1")
check("assembly and file versions remain synchronized", assembly_match and file_match and assembly_match.group(1) == file_match.group(1))
check("frontend and backend use matching Stage 8 build codes", re.search(r"APP_BUILD_CODE = '([^']+)'", frontend_version).group(1) == re.search(r'const string BuildCode = "([^"]+)"', backend_version).group(1) and "GARMETIX-8" in frontend_version)

failed = [name for name, passed in checks if not passed]
for name, passed in checks:
    print(f"[{'PASS' if passed else 'FAIL'}] {name}")

if failed:
    raise SystemExit(f"{len(failed)} Stage 8A Package 3 check(s) failed")

print(f"Stage 8A Package 3 static validation passed: {len(checks)} checks")

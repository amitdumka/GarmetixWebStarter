import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = []


def read(path: str) -> str:
    return (root / path).read_text(encoding="utf-8")


def check(name: str, condition: bool):
    checks.append((name, condition))


reports = read("frontend/garmetix-web/pages/reports/index.vue")
gst_reports = read("frontend/garmetix-web/pages/gst-reports/index.vue")
import_export = read("frontend/garmetix-web/pages/import-export/index.vue")
audit = read("frontend/garmetix-web/pages/audit/index.vue")
message_logs = read("frontend/garmetix-web/pages/message-logs/index.vue")
audit_progress = read("frontend/garmetix-web/composables/useUiAuditProgress.ts")
frontend_version = read("frontend/garmetix-web/utils/appVersion.ts")
backend_version = read("backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs")
project = read("backend/Garmetix.Api/Garmetix.Api.csproj")
package = json.loads(read("frontend/garmetix-web/package.json"))
package_lock = json.loads(read("frontend/garmetix-web/package-lock.json"))

check("Reports uses a retryable register", "UiRegisterPanel" in reports and ':error="loadError"' in reports and '@retry="refresh"' in reports)
check("Reports defaults preserve local dates", "function localDateInput" in reports and "today.toISOString().slice(0, 10)" not in reports)
check("GST reports use retryable registers", gst_reports.count("UiRegisterPanel") >= 3 and gst_reports.count(':error="loadError"') >= 3)
check("GST reports send the selected company", "useWorkspace()" in gst_reports and "params.set('companyId', selectedCompanyId.value)" in gst_reports)
check("Import Export uses a retryable module register", "UiRegisterPanel" in import_export and ':error="loadError"' in import_export)
check("Audit Trail uses a retryable activity register", "UiRegisterPanel" in audit and ':error="loadError"' in audit)
check("Message Logs uses a retryable backend register", "UiRegisterPanel" in message_logs and ':error="loadError"' in message_logs)
check("Message Log selects avoid empty values", "ALL_FILTER_VALUE" in message_logs and "value: ''" not in message_logs)
check("Message Log cards use compact radii", "rounded-3xl" not in message_logs and "rounded-2xl" not in message_logs)
check("load errors are retained on all package pages", all("const loadError = ref('')" in page for page in (reports, gst_reports, import_export, audit, message_logs)))
check("register errors use sanitized feedback messages", all("feedback.errorMessage(" in page for page in (reports, gst_reports, import_export, audit, message_logs)))
check("audit storage migrates from v4.0.7", "garmetix.ui-audit.v4.0.8" in audit_progress and "garmetix.ui-audit.v4.0.7" in audit_progress)
check("Package 10 routes are reviewed", all(f"'{route}'" in audit_progress for route in ("/reports", "/gst-reports", "/import-export", "/audit", "/message-logs")))

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
runtime_version = next(iter(versions)) if len(versions) == 1 else ""
check("all runtime versions remain synchronized in v4", bool(runtime_version) and runtime_version.startswith("4."))
check("assembly and file versions match the runtime version", assembly_match and file_match and assembly_match.group(1) == file_match.group(1) == f"{runtime_version}.0")
check("frontend and backend use matching Stage 8 build codes", re.search(r"APP_BUILD_CODE = '([^']+)'", frontend_version).group(1) == re.search(r'const string BuildCode = "([^"]+)"', backend_version).group(1) and "GARMETIX-8" in frontend_version)

failed = [name for name, passed in checks if not passed]
for name, passed in checks:
    print(f"[{'PASS' if passed else 'FAIL'}] {name}")

if failed:
    raise SystemExit(f"{len(failed)} Stage 8A Package 10 check(s) failed")

print(f"Stage 8A Package 10 static validation passed: {len(checks)} checks")

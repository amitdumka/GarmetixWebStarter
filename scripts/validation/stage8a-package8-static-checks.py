import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = []


def read(path: str) -> str:
    return (root / path).read_text(encoding="utf-8")


def check(name: str, condition: bool):
    checks.append((name, condition))


payroll_api = read("backend/Garmetix.Api/Payroll/PayrollEndpoints.cs")
payroll_service = read("backend/Garmetix.Api/Payroll/PayrollService.cs")
payroll_dtos = read("backend/Garmetix.Api/Payroll/PayrollDtos.cs")
numbering = read("backend/Garmetix.Api/Numbering/DocumentNumberService.cs")
payroll_page = read("frontend/garmetix-web/pages/payroll/index.vue")
audit_progress = read("frontend/garmetix-web/composables/useUiAuditProgress.ts")
frontend_version = read("frontend/garmetix-web/utils/appVersion.ts")
backend_version = read("backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs")
project = read("backend/Garmetix.Api/Garmetix.Api.csproj")
package = json.loads(read("frontend/garmetix-web/package.json"))
package_lock = json.loads(read("frontend/garmetix-web/package-lock.json"))

check("salary payment uses a dedicated save request", "SalaryPaymentUpsertRequest" in payroll_dtos and "SalaryPayment request" not in payroll_api)
check("salary payment preview endpoint is mapped", 'MapPost("/preview", PreviewSalaryPaymentAsync)' in payroll_api)
check("preview calculates advance, prior due and already paid", all(value in payroll_service for value in ("salaryAdvance", "previousDue", "alreadyPaid", "totalDeductions")))
check("deleted salary rows are excluded from calculations", payroll_service.count("!payment.Deleted") >= 3)
check("salary payment amount is rounded to whole rupees", "RoundRupee(request.Amount)" in payroll_api)
check("rounded payroll liability closes paise-only balances", "var payable = RoundRupee" in payroll_service and "var roundedPayable = RoundRupee(netPayable)" in payroll_service)
check("fully paid salary rejects duplicate payment", "ValidateOutstandingSalaryAsync" in payroll_api and "already fully paid" in payroll_api)
check("salary payment transactions use the retry execution strategy", payroll_api.count("CreateExecutionStrategy()") >= 3)
check("salary payment number is server generated", "NextSalaryPaymentAsync" in payroll_api and "NextSalaryPaymentAsync" in numbering)
check("SPAY format includes store and month", 'onDate:yyyyMM}/SPAY/{numericPart}' in numbering)
check("frontend requests authoritative preview", "salary-payments/preview" in payroll_page and "precalculatePayment" in payroll_page)
check("frontend displays payroll adjustments", all(value in payroll_page for value in ("Advance deducted", "Previous due added", "Already paid", "Round off")))
check("frontend no longer invents payment voucher numbers", "createPayrollVoucherNumber" not in payroll_page and "'Assigned on save'" in payroll_page)
check("payment date is timezone neutral", 'return `${value}T00:00:00`' in payroll_page)
check("audit storage migrates from v4.0.5", "garmetix.ui-audit.v4.0.6" in audit_progress and "garmetix.ui-audit.v4.0.5" in audit_progress)

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
check("all runtime versions are 4.0.6", versions == {"4.0.6"})
check("assembly and file versions are 4.0.6.0", assembly_match and file_match and assembly_match.group(1) == file_match.group(1) == "4.0.6.0")
check("frontend and backend build codes match", "GARMETIX-8A-20260613-4006" in frontend_version and "GARMETIX-8A-20260613-4006" in backend_version)

failed = [name for name, passed in checks if not passed]
for name, passed in checks:
    print(f"[{'PASS' if passed else 'FAIL'}] {name}")

if failed:
    raise SystemExit(f"{len(failed)} Stage 8A Package 8 check(s) failed")

print(f"Stage 8A Package 8 static validation passed: {len(checks)} checks")

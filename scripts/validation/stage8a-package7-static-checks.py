import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = []


def read(path: str) -> str:
    return (root / path).read_text(encoding="utf-8")


def check(name: str, condition: bool):
    checks.append((name, condition))


document_codes = read("backend/Garmetix.Api/ProductLookup/DocumentCodeService.cs")
lookup = read("backend/Garmetix.Api/ProductLookup/ProductLookupEndpoints.cs")
reset = read("backend/Garmetix.Api/Backup/FactoryResetEndpoints.cs")
program = read("backend/Garmetix.Api/Program.cs")
pdf_print = read("frontend/garmetix-web/composables/useServerDocumentPrint.ts")
scanner = read("frontend/garmetix-web/pages/document-scan/index.vue")
app_shell = read("frontend/garmetix-web/components/AppShell.vue")
access = read("frontend/garmetix-web/composables/useAccessControl.ts")
system_health = read("frontend/garmetix-web/pages/system-health/index.vue")
inventory = read("frontend/garmetix-web/pages/inventory/index.vue")
stock = read("frontend/garmetix-web/pages/stock-operations/index.vue")
payroll_api = read("backend/Garmetix.Api/Payroll/PayrollEndpoints.cs")
petty_api = read("backend/Garmetix.Api/Accounting/PettyCashEndpoints.cs")
payroll_pdf = read("backend/Garmetix.Api/Payroll/PayrollPdfDocument.cs")
petty_pdf = read("backend/Garmetix.Api/Accounting/PettyCashPdfDocument.cs")
frontend_version = read("frontend/garmetix-web/utils/appVersion.ts")
backend_version = read("backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs")
project = read("backend/Garmetix.Api/Garmetix.Api.csproj")
package = json.loads(read("frontend/garmetix-web/package.json"))
package_lock = json.loads(read("frontend/garmetix-web/package-lock.json"))

document_types = (
    "SaleInvoice",
    "PurchaseInvoice",
    "Voucher",
    "CashVoucher",
    "CommercialNote",
    "PettyCash",
    "Payslip",
    "SalaryPayment",
)
check("document codes use stable GMX tokens", '"GMX:{NormalizeType(documentType)}:{id:N}"' in document_codes)
check("all requested document types are registered", all(name in document_codes for name in document_types))
check("QR SVG and PDF renderers are available", "BuildSvg" in document_codes and "AppendPdfCommands" in document_codes)
check("scan lookup supports every document type", all(f"DocumentCodeService.{name}" in lookup for name in document_types))
check("scanner accepts manual and camera input", "BarcodeDetector" in scanner and "getUserMedia" in scanner and "const code = ref('')" in scanner)
check("scanner is present in navigation and access control", "/document-scan" in app_shell and "/document-scan" in access)

check("factory reset is Admin protected", "GarmetixPolicies.Admin" in reset)
check("factory reset requires typed confirmation", '"FACTORY RESET"' in reset and "factoryResetConfirmation" in system_health)
check("factory reset creates a safety backup", '"pre-factory-reset"' in reset)
check("factory reset preserves migrations and current admin", "__EFMigrationsHistory" in reset and "preservedAdmin" in reset)
check("factory reset endpoint is mapped", "MapFactoryResetEndpoints" in program)

check("server PDF printing validates the response", "application/pdf" in pdf_print and "blob.size" in pdf_print)
check("server PDF printing uses a PDF iframe", "iframe" in pdf_print and "contentWindow?.print()" in pdf_print)
check("petty cash exposes a server PDF", '/{id:guid}/pdf' in petty_api and "PettyCashPdfDocument" in petty_api)
check("payroll exposes payslip and salary-payment PDFs", '"/payslips/{id:guid}/pdf"' in payroll_api and '"/{id:guid}/pdf"' in payroll_api and '"/api/salary-payments"' in payroll_api)
check("petty cash PDF is colored A5 landscape with QR", all(value in petty_pdf for value in ("595.28", "419.53", "DocumentCodeService", "Receipts", "Payments")))
check("payroll PDFs include QR and salary deductions", all(value in payroll_pdf for value in ("DocumentCodeService", "Salary advance adjusted", "Previous due", "Net salary")))

print_pages = (
    "frontend/garmetix-web/pages/billing/index.vue",
    "frontend/garmetix-web/pages/purchase/index.vue",
    "frontend/garmetix-web/pages/vouchers/index.vue",
    "frontend/garmetix-web/pages/cash-vouchers/index.vue",
    "frontend/garmetix-web/components/CommercialNoteEntryForm.vue",
    "frontend/garmetix-web/pages/petty-cash/index.vue",
    "frontend/garmetix-web/pages/payroll/index.vue",
)
print_sources = [read(path) for path in print_pages]
check("printable modules use authenticated server PDF printing", all("documentPrint.printPdf" in source for source in print_sources))
check("printable modules do not print the dashboard DOM", all("window.print()" not in source for source in print_sources))

check("inventory uses shared register and wide workspace", "UiRegisterPanel" in inventory and "sm:max-w-5xl xl:max-w-6xl" in inventory)
check("inventory optional vendor avoids invalid empty select value", "NO_VENDOR_VALUE" in inventory and "{ value: '', label: 'No vendor selected' }" not in inventory)
check("stock operations uses shared searchable register", "UiRegisterPanel" in stock and "movementSearch" in stock)

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
check("runtime versions remain synchronized after v4.0.5", len(versions) == 1 and next(iter(versions)) >= "4.0.5")
check("assembly and file versions remain synchronized", assembly_match and file_match and assembly_match.group(1) == file_match.group(1))
check("frontend and backend use matching Stage 8 build codes", re.search(r"APP_BUILD_CODE = '([^']+)'", frontend_version).group(1) == re.search(r'const string BuildCode = "([^"]+)"', backend_version).group(1) and "GARMETIX-8" in frontend_version)

failed = [name for name, passed in checks if not passed]
for name, passed in checks:
    print(f"[{'PASS' if passed else 'FAIL'}] {name}")

if failed:
    raise SystemExit(f"{len(failed)} Stage 8A Package 7 check(s) failed")

print(f"Stage 8A Package 7 static validation passed: {len(checks)} checks")

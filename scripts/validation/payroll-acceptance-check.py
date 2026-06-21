from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks: list[tuple[str, bool]] = []


def read(path: str) -> str:
    return (root / path).read_text(encoding="utf-8")


def add(name: str, ok: bool):
    checks.append((name, ok))


page = read("frontend/garmetix-web/pages/payroll/index.vue")
endpoints = read("backend/Garmetix.Api/Payroll/PayrollEndpoints.cs")
service = read("backend/Garmetix.Api/Payroll/PayrollService.cs")
pdf = read("backend/Garmetix.Api/Payroll/PayrollPdfDocument.cs")
posting = read("backend/Garmetix.Api/Accounting/AccountingPostingService.cs")
numbering = read("backend/Garmetix.Api/Numbering/DocumentNumberService.cs")
app_info = read("backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs")
app_version = read("frontend/garmetix-web/utils/appVersion.ts")
csproj = read("backend/Garmetix.Api/Garmetix.Api.csproj")

version_identity = (
    all(token in app_info for token in ['Version = "4.10.27"', "Payroll PDF Download Guard", "GARMETIX-10J-20260620-4127"])
    and "APP_VERSION = '4.10.27'" in app_version
    and "<Version>4.10.27</Version>" in csproj
) or (
    all(token in app_info for token in ['Version = "4.10.28"', "Import Export Transfer Guard", "GARMETIX-10J-20260620-4128"])
    and "APP_VERSION = '4.10.28'" in app_version
    and "<Version>4.10.28</Version>" in csproj
) or (
    all(token in app_info for token in ['Version = "4.10.29"', "Stage 10K Production Operator Acceptance", "GARMETIX-10K-20260620-4129"])
    and "APP_VERSION = '4.10.29'" in app_version
    and "<Version>4.10.29</Version>" in csproj
) or (
    all(token in app_info for token in ['Version = "4.10.30"', "Stage 10L Production Support Pack", "GARMETIX-10L-20260620-4130"])
    and "APP_VERSION = '4.10.30'" in app_version
    and "<Version>4.10.30</Version>" in csproj
) or (
    all(token in app_info for token in ['Version = "4.10.31"', "Stage 10M Production Rehearsal Tracker", "GARMETIX-10M-20260620-4131"])
    and "APP_VERSION = '4.10.31'" in app_version
    and "<Version>4.10.31</Version>" in csproj
 ) or (
    all(token in app_info for token in ['Version = "4.11.0"', "Stage 11A MAUI Android Attendance Kiosk Shell", "GARMETIX-11A-20260621-4110"])
    and "APP_VERSION = '4.11.0'" in app_version
    and "<Version>4.11.0</Version>" in csproj
)
add("version identity", version_identity)
add(
    "salary payment download action",
    all(token in page for token in [
        "label: 'Download'",
        "onClick: () => downloadSalaryPayment(row.original.raw)",
        "async function downloadSalaryPayment(payment: any)",
        "documentPrint.downloadPdf(`salary-payments/${payment.id}/pdf`, fileName)",
        "feedback.notify('Salary payment PDF downloaded')",
    ]),
)
add(
    "pdf filenames are safe",
    all(token in page for token in [
        "function safePdfName(value: unknown, fallback: string)",
        ".replace(/[\\\\/:*?\"<>|]+/g, '-')",
        "salary-payment-${payment.id}",
        "safePdfName(`payslip-${selectedPayslip.value.monthYear",
    ])
    and all(token in endpoints for token in [
        "SafePdfFileName(payment.VoucherNumber, \"salary-payment\")",
        "private static string SafePdfFileName",
        "char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-'",
        "StringSplitOptions.RemoveEmptyEntries",
    ]),
)
add(
    "salary payment save and print flow",
    all(token in page for token in [
        "salary-payments/preview",
        "precalculatePayment",
        "createdPayment = await api.create<any>('salary-payments', payload)",
        "await printSalaryPayment(createdPayment)",
        "amount: Number(paymentForm.amount || 0)",
        "@blur=\"paymentForm.amount = Math.round(Number(paymentForm.amount || 0))\"",
    ])
    and all(token in endpoints for token in [
        "ValidateOutstandingSalaryAsync(request, null, payroll",
        "RoundRupee(request.Amount)",
        "ApplySalaryPayment(payment, request)",
        "payment.Amount = RoundRupee(request.Amount)",
    ]),
)
add(
    "advance due and already paid preview",
    all(token in service for token in [
        "CalculateBenefitAdvanceAsync",
        "CalculateCarryForwardDueAsync",
        "salaryAdvance",
        "alreadyPaid",
        "previousDue",
        "netPayable = Math.Max(0, grossSalary - totalDeductions + previousDue)",
        "roundedAmount",
        "RoundMoney(roundedPayable - netPayable)",
    ])
    and all(token in page for token in [
        "Advance deducted",
        "Previous due added",
        "Already paid",
        "Round off",
        "Balance after payment",
    ]),
)
add(
    "server numbering and accounting posting",
    all(token in endpoints for token in [
        "documentNumbers.NextSalaryPaymentAsync",
        "payment.VoucherNumber",
        "db.SalaryPayments.Add(payment)",
        "accounting.PostSalaryPaymentAsync(payment",
    ])
    and all(token in numbering for token in [
        '"SalaryPayment"',
        '"SPAY"',
        'return $"{(safeStoreCode.Length > 0 ? safeStoreCode : "STORE")}/{onDate:yyyyMM}/SPAY/{numericPart}"',
    ])
    and all(token in posting for token in [
        "PostSalaryPaymentAsync",
        "EnsureEmployeePartyAsync",
        "Salary Payables",
        "Salary Advance",
        "Cash In Hand",
        "Bank Clearing",
        "RepostSourceJournalAsync",
    ]),
)
add(
    "payslip generation and pdf share remain guarded",
    all(token in page for token in [
        "autoGeneratePayrollIfDue",
        "today.getDate() !== 1",
        "payroll/payslips/${selectedPayslip.value.id}/pdf",
        "sharePayslipEmail",
        "sharePayslipWhatsApp",
    ])
    and all(token in pdf for token in [
        "BuildPayslip",
        "BuildSalaryPayment",
        "DocumentCodeService.Payslip",
        "DocumentCodeService.SalaryPayment",
        "SALARY PAYMENT SLIP",
    ]),
)

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(("PASS" if ok else "FAIL") + f": {name}")
if failed:
    raise SystemExit("Payroll acceptance validation failed: " + ", ".join(failed))
print("Payroll acceptance validation passed.")

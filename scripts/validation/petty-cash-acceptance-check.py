from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks: list[tuple[str, bool]] = []


def read(path: str) -> str:
    return (root / path).read_text(encoding="utf-8")


def add(name: str, ok: bool):
    checks.append((name, ok))


pdf = read("backend/Garmetix.Api/Accounting/PettyCashPdfDocument.cs")
endpoints = read("backend/Garmetix.Api/Accounting/PettyCashEndpoints.cs")
page = read("frontend/garmetix-web/pages/petty-cash/index.vue")
app_info = read("backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs")
app_version = read("frontend/garmetix-web/utils/appVersion.ts")
csproj = read("backend/Garmetix.Api/Garmetix.Api.csproj")

version_identity = (
    all(token in app_info for token in ['Version = "4.10.25"', "Petty Cash PDF Pagination Guard", "GARMETIX-10J-20260620-4125"])
    and "APP_VERSION = '4.10.25'" in app_version
    and "<Version>4.10.25</Version>" in csproj
) or (
    all(token in app_info for token in ['Version = "4.10.26"', "Voucher PDF Download Guard", "GARMETIX-10J-20260620-4126"])
    and "APP_VERSION = '4.10.26'" in app_version
    and "<Version>4.10.26</Version>" in csproj
) or (
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
) or (
    all(token in app_info for token in ['Version = "4.11.1"', "Stage 11A Android Build Hardening", "GARMETIX-11A-20260621-4111"])
    and "APP_VERSION = '4.11.1'" in app_version
    and "<Version>4.11.1</Version>" in csproj
) or (
    all(token in app_info for token in ['Version = "4.11.2"', "Stage 11A Physical Tablet Rehearsal", "GARMETIX-11A-20260621-4112"])
    and "APP_VERSION = '4.11.2'" in app_version
    and "<Version>4.11.2</Version>" in csproj
)
add("version identity", version_identity)
add(
    "server pdf paginates all detail rows",
    all(token in pdf for token in [
        "DrawTransactionDetailPages",
        "rowsPerPage",
        "Math.Ceiling(lines.Count / (double)rowsPerPage)",
        "Skip(pageNumber * rowsPerPage)",
        "Take(rowsPerPage)",
        "Page {pageNumber} of {pageCount}",
        "Continued on next A5 detail page",
        "Transaction totals",
    ])
    and ".Take(13)" not in pdf
    and "not printed due A5 space" not in pdf,
)
add(
    "server pdf keeps color and qr",
    all(token in pdf for token in [
        "canvas.Fill(left, top, bodyWidth, 52, 0.02, 0.09, 0.16)",
        "canvas.Fill(left, top + 52, bodyWidth, 3, 0.02, 0.70, 0.64)",
        "DocumentCodeService.Create(DocumentCodeService.PettyCash, sheet.Id)",
        "canvas.Qr",
        "CLOSING CASH IN HAND",
    ]),
)
add(
    "server endpoint includes transaction details",
    all(token in endpoints for token in [
        'group.MapGet("/{id:guid}/pdf", DownloadPdfAsync)',
        "BuildTransactionDetailLinesAsync",
        "PettyCashPdfDocument.Build(sheet",
        "Cash Sale",
        "Due Receipt",
        "Voucher",
        "Cash Voucher",
        "Bank Withdrawal",
        "Bank Deposit",
    ]),
)
add(
    "opening balance and cash in hand behavior",
    all(token in endpoints for token in [
        "previousDate = dayStart.AddDays(-1)",
        "previousSheet?.CashInHand ?? 0m",
        "OpeningBalanceSource",
        "entity.CashInHand = CalculateCashInHand(entity)",
        "FindDifferences(entity, expected)",
    ])
    and all(token in page for token in [
        "latestSheet",
        "cashInHand: latestSheet.value ? sheetCash(latestSheet.value) : 0",
        "watch(calculatedCash",
        "form.cashInHand = roundMoney(value)",
    ]),
)
add(
    "mismatch alert and print flow",
    all(token in endpoints for token in [
        "WriteMismatchAlertAsync",
        "ReconciliationMismatch",
        "MismatchEmailFailed",
        "reconciliationAlert",
        "alertDeliveryFailed",
    ])
    and all(token in page for token in [
        "reconciliationDifferences",
        "The sheet was saved and an owner alert was added to Message Logs.",
        "documentPrint.printPdf(`petty-cash-sheets/${sheet.id}/pdf`)",
        "Print A5",
    ]),
)

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(("PASS" if ok else "FAIL") + f": {name}")
if failed:
    raise SystemExit("Petty cash acceptance validation failed: " + ", ".join(failed))
print("Petty cash acceptance validation passed.")

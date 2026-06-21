from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks: list[tuple[str, bool]] = []


def read(path: str) -> str:
    return (root / path).read_text(encoding="utf-8")


def add(name: str, ok: bool):
    checks.append((name, ok))


page = read("frontend/garmetix-web/pages/vouchers/index.vue")
posting = read("backend/Garmetix.Api/Accounting/AccountingPostingService.cs")
endpoints = read("backend/Garmetix.Api/Accounting/AccountingEndpoints.cs")
pdf = read("backend/Garmetix.Api/Accounting/VoucherPdfDocument.cs")
numbering = read("backend/Garmetix.Api/Numbering/DocumentNumberService.cs")
app_info = read("backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs")
app_version = read("frontend/garmetix-web/utils/appVersion.ts")
csproj = read("backend/Garmetix.Api/Garmetix.Api.csproj")

version_identity = (
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
    all(token in app_info for token in ['Version = "4.11.3"', "Stage 11B Fingerprint Bridge Contract", "GARMETIX-11B-20260621-4113"])
    and "APP_VERSION = '4.11.3'" in app_version
    and "<Version>4.11.3</Version>" in csproj
)
add("version identity", version_identity)
add(
    "hosted-safe voucher pdf download",
    "documentPrint.downloadPdf(" in page
    and "`vouchers/${selectedPrintVoucher.value.id}/pdf?${query.toString()}`" in page
    and "feedback.notify('Voucher PDF downloaded')" in page
    and "${config.public.apiBase}/vouchers/" not in page
    and "const config = useRuntimeConfig()" not in page,
)
add(
    "create prints while edit only saves",
    all(token in page for token in [
        "createdVoucher = await api.create<any>('vouchers', payload)",
        "await api.update<any>('vouchers', form.id, payload)",
        "if (createdVoucher?.id)",
        "openPrintVoucher(createdVoucher)",
        "await printVoucher()",
    ]),
)
add(
    "frontend keeps party ledger internal",
    all(token in page for token in [
        "const partyLedger = parties.value.find((item) => item.ledgerId === form.ledgerId)",
        "partyId: null",
        "ledgerId: nullableGuid(form.ledgerId)",
    ])
    and "Party ledger" not in page
    and "isParty" not in page.split("<template>", 1)[1],
)
add(
    "local date and voucher number flow",
    all(token in page for token in [
        "onDate: accountingDateTimeForApi(form.onDate)",
        "function accountingDateTimeForApi(value: unknown)",
        "return `${dateInputValue(value)}T00:00:00`",
        "Generated after save",
    ])
    and all(token in posting for token in [
        "voucher.OnDate = request.OnDate.Date",
        "documentNumbers.NextVoucherAsync",
    ])
    and all(token in numbering for token in [
        'DocumentNumberGenerator.NextAsync(',
        '"Voucher"',
        '"VCH"',
        'return $"{(safeStoreCode.Length > 0 ? safeStoreCode : "STORE")}/{onDate:yyyyMM}/{numericPart}"',
    ]),
)
add(
    "bank account and cheque audit safety",
    all(token in page for token in [
        "requiresBankAccount",
        "Select bank account for non-cash voucher.",
        "accountNumber: requiresBankAccount.value ? nullableGuid(form.accountNumber) : null",
    ])
    and all(token in posting for token in [
        "ResolveBankAccountAsync(request",
        "ResolveCashOrBankLedgerAsync(request, bankAccount",
        "UpsertBankTransactionAsync(voucher, bankAccount",
        "UpsertChequeLogAsync(voucher, bankAccount",
    ]),
)
add(
    "server owns party link and audit immutability",
    all(token in posting for token in [
        "ResolveVoucherPartyAsync(request, ledger",
        "voucher.IsParty = ledger.IsParty || party is not null",
        "voucher.PartyId = party?.Id",
        "Converted vouchers are immutable",
    ])
    and all(token in endpoints for token in [
        "CashVoucherConversions.AnyAsync(item => item.VoucherId == id",
        "Converted vouchers are retained for audit and cannot be deleted.",
    ]),
)
add(
    "server pdf has qr color and print options",
    all(token in endpoints for token in [
        'vouchers.MapGet("/{id:guid}/pdf", DownloadVoucherPdfAsync)',
        "DocumentCodeService.Voucher",
        "VoucherPdfDocument.Build(",
        'string.Equals(format, "a5-one"',
        "reprint == true",
        "signatures != false",
    ])
    and all(token in pdf for token in [
        "private const double A5Width",
        "private const double A4Width",
        "canvas.FillRect(left, top, width, 46",
        "canvas.FillRect(left, top + 46, width, 3",
        "canvas.Qr(model.DocumentCode",
        "REPRINT",
        "Prepared by",
        "Received by",
    ]),
)

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(("PASS" if ok else "FAIL") + f": {name}")
if failed:
    raise SystemExit("Voucher acceptance validation failed: " + ", ".join(failed))
print("Voucher acceptance validation passed.")

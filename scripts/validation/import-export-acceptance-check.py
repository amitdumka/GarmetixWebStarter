from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks: list[tuple[str, bool]] = []


def read(path: str) -> str:
    return (root / path).read_text(encoding="utf-8")


def add(name: str, ok: bool):
    checks.append((name, ok))


page = read("frontend/garmetix-web/pages/import-export/index.vue")
endpoints = read("backend/Garmetix.Api/ImportExport/ImportExportEndpoints.cs")
stage10j = read("scripts/validation/stage10j-import-export-engine-check.py")
current_release = read("scripts/validation/current-release-checks.py")
app_info = read("backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs")
app_version = read("frontend/garmetix-web/utils/appVersion.ts")
csproj = read("backend/Garmetix.Api/Garmetix.Api.csproj")

add(
    "version identity",
    (
        all(token in app_info for token in ['Version = "4.10.28"', "Import Export Transfer Guard", "GARMETIX-10J-20260620-4128"])
        and "APP_VERSION = '4.10.28'" in app_version
        and "<Version>4.10.28</Version>" in csproj
    )
    or (
        all(token in app_info for token in ['Version = "4.10.29"', "Stage 10K Production Operator Acceptance", "GARMETIX-10K-20260620-4129"])
        and "APP_VERSION = '4.10.29'" in app_version
        and "<Version>4.10.29</Version>" in csproj
    )
    or (
        all(token in app_info for token in ['Version = "4.10.30"', "Stage 10L Production Support Pack", "GARMETIX-10L-20260620-4130"])
        and "APP_VERSION = '4.10.30'" in app_version
        and "<Version>4.10.30</Version>" in csproj
    )
    or (
        all(token in app_info for token in ['Version = "4.10.31"', "Stage 10M Production Rehearsal Tracker", "GARMETIX-10M-20260620-4131"])
        and "APP_VERSION = '4.10.31'" in app_version
        and "<Version>4.10.31</Version>" in csproj
    )
    or (
        all(token in app_info for token in ['Version = "4.11.0"', "Stage 11A MAUI Android Attendance Kiosk Shell", "GARMETIX-11A-20260621-4110"])
        and "APP_VERSION = '4.11.0'" in app_version
        and "<Version>4.11.0</Version>" in csproj
    )
    or (
        all(token in app_info for token in ['Version = "4.11.1"', "Stage 11A Android Build Hardening", "GARMETIX-11A-20260621-4111"])
        and "APP_VERSION = '4.11.1'" in app_version
        and "<Version>4.11.1</Version>" in csproj
    )
    or (
        all(token in app_info for token in ['Version = "4.11.16"', "Stage 11D-1 Migration Startup Guard", "GARMETIX-11D1-20260622-4116"])
        and "APP_VERSION = '4.11.16'" in app_version
        and "<Version>4.11.16</Version>" in csproj
    ),
)
add(
    "hosted-safe transfer urls",
    "const fileTransfer = useServerDocumentPrint()" in page
    and "fileTransfer.apiUrl(`import-export/${kind}/${moduleKey}`)" in page
    and "fileTransfer.apiUrl(`import-export/import/${selectedModule.value}?commit=${commit}`)" in page
    and "config.public.apiBase" not in page
    and "useRuntimeConfig()" not in page,
)
add(
    "csv filename safety",
    all(token in page for token in [
        "parseContentDispositionFileName(disposition)",
        "function parseContentDispositionFileName(disposition: string)",
        "filename\\*=UTF-8''",
        "function safeCsvName(value: unknown, fallback: string)",
        ".replace(/[\\\\/:*?\"<>|]+/g, '-')",
        "function downloadBlob(blob: Blob, fileName: string)",
        "downloadBlob(blob, fileName)",
        "downloadBlob(blob, safeCsvName(`Garmetix-${selectedModule.value}-import-errors`, 'Garmetix-import-errors'))",
    ]),
)
add(
    "safe validate then commit flow",
    all(token in page for token in [
        "Validate CSV",
        "Import CSV",
        ":disabled=\"!selectedFile || !selectedModuleInfo?.importSupported || Boolean(importResult?.errors?.length)\"",
        "uploadCsv(false)",
        "uploadCsv(true)",
        "feedback.failed(commit ? 'Import blocked' : 'Validation failed'",
    ])
    and all(token in endpoints for token in [
        'group.MapPost("/import/{module}", ImportModuleAsync).DisableAntiforgery()',
        "commit",
        "ImportResult",
        "Errors",
        "Warnings",
    ]),
)
add(
    "real import export engine preserved",
    all(token in endpoints for token in [
        '["products"]',
        '["customers"]',
        '["vendors"]',
        '["stock-opening"]',
        "ImportProductsAsync",
        "ImportCustomersAsync",
        "ImportVendorsAsync",
        "ImportStockOpeningAsync",
        "StockOpeningImportAdjustment",
        "stockLedger.PostAsync",
    ])
    and all(token in page for token in [
        "products:",
        "customers:",
        "vendors:",
        "'stock-opening':",
        "Real import engine enabled",
    ]),
)
add(
    "release validation coverage",
    "import-export-acceptance-check.py" in current_release
    and "Stage 10J import/export engine validation passed." in stage10j,
)

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(("PASS" if ok else "FAIL") + f": {name}")
if failed:
    raise SystemExit("Import/export acceptance validation failed: " + ", ".join(failed))
print("Import/export acceptance validation passed.")

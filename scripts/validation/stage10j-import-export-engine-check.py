from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks: list[tuple[str, bool]] = []

def read(path: str) -> str:
    return (root / path).read_text(encoding='utf-8')

def exists(path: str) -> bool:
    return (root / path).exists()

def add(name: str, ok: bool):
    checks.append((name, ok))

app_info = read('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs')
app_version = read('frontend/garmetix-web/utils/appVersion.ts')
csproj = read('backend/Garmetix.Api/Garmetix.Api.csproj')
import_export = read('backend/Garmetix.Api/ImportExport/ImportExportEndpoints.cs')
import_page = read('frontend/garmetix-web/pages/import-export/index.vue')
app_shell = read('frontend/garmetix-web/components/AppShell.vue')
legacy_shell = read('frontend/garmetix-web/components/AppShellLegacy.vue')
main_css = read('frontend/garmetix-web/assets/css/main.css')
current_release = read('scripts/validation/current-release-checks.py')
drill = read('scripts/linux/stage10j-import-export-engine-drill.sh') if exists('scripts/linux/stage10j-import-export-engine-drill.sh') else ''

version_identity = (
    all(token in app_info for token in ['Version = "4.10.17"', 'Stage 10J Real Excel Import Export Engine', 'GARMETIX-10J-20260620-4117'])
    and "APP_VERSION = '4.10.17'" in app_version
    and '<Version>4.10.17</Version>' in csproj
) or (
    all(token in app_info for token in ['Version = "4.10.18"', 'Stage 10J Real Excel Import Export Engine', 'GARMETIX-10J-20260620-4118'])
    and "APP_VERSION = '4.10.18'" in app_version
    and '<Version>4.10.18</Version>' in csproj
) or (
    all(token in app_info for token in ['Version = "4.10.19"', 'Stage 10J Real Excel Import Export Engine', 'GARMETIX-10J-20260620-4119'])
    and "APP_VERSION = '4.10.19'" in app_version
    and '<Version>4.10.19</Version>' in csproj
) or (
    all(token in app_info for token in ['Version = "4.10.20"', 'Stage 10J Real Excel Import Export Engine', 'GARMETIX-10J-20260620-4120'])
    and "APP_VERSION = '4.10.20'" in app_version
    and '<Version>4.10.20</Version>' in csproj
) or (
    all(token in app_info for token in ['Version = "4.10.21"', 'Stage 10J Real Excel Import Export Engine', 'GARMETIX-10J-20260620-4121'])
    and "APP_VERSION = '4.10.21'" in app_version
    and '<Version>4.10.21</Version>' in csproj
) or (
    all(token in app_info for token in ['Version = "4.10.22"', 'Stage 10J Real Excel Import Export Engine', 'GARMETIX-10J-20260620-4122'])
    and "APP_VERSION = '4.10.22'" in app_version
    and '<Version>4.10.22</Version>' in csproj
) or (
    all(token in app_info for token in ['Version = "4.10.23"', 'Stage 10J Real Excel Import Export Engine', 'GARMETIX-10J-20260620-4123'])
    and "APP_VERSION = '4.10.23'" in app_version
    and '<Version>4.10.23</Version>' in csproj
) or (
    all(token in app_info for token in ['Version = "4.10.24"', 'Stage 10J Real Excel Import Export Engine', 'GARMETIX-10J-20260620-4124'])
    and "APP_VERSION = '4.10.24'" in app_version
    and '<Version>4.10.24</Version>' in csproj
) or (
    all(token in app_info for token in ['Version = "4.10.25"', 'Stage 10J Real Excel Import Export Engine', 'GARMETIX-10J-20260620-4125'])
    and "APP_VERSION = '4.10.25'" in app_version
    and '<Version>4.10.25</Version>' in csproj
) or (
    all(token in app_info for token in ['Version = "4.10.26"', 'Stage 10J Real Excel Import Export Engine', 'GARMETIX-10J-20260620-4126'])
    and "APP_VERSION = '4.10.26'" in app_version
    and '<Version>4.10.26</Version>' in csproj
) or (
    all(token in app_info for token in ['Version = "4.10.27"', 'Stage 10J Real Excel Import Export Engine', 'GARMETIX-10J-20260620-4127'])
    and "APP_VERSION = '4.10.27'" in app_version
    and '<Version>4.10.27</Version>' in csproj
) or (
    all(token in app_info for token in ['Version = "4.10.28"', 'Stage 10J Real Excel Import Export Engine', 'GARMETIX-10J-20260620-4128'])
    and "APP_VERSION = '4.10.28'" in app_version
    and '<Version>4.10.28</Version>' in csproj
) or (
    all(token in app_info for token in ['Version = "4.10.29"', 'Stage 10K Production Operator Acceptance', 'GARMETIX-10K-20260620-4129'])
    and "APP_VERSION = '4.10.29'" in app_version
    and '<Version>4.10.29</Version>' in csproj
) or (
    all(token in app_info for token in ['Version = "4.10.30"', 'Stage 10L Production Support Pack', 'GARMETIX-10L-20260620-4130'])
    and "APP_VERSION = '4.10.30'" in app_version
    and '<Version>4.10.30</Version>' in csproj
) or (
    all(token in app_info for token in ['Version = "4.10.31"', 'Stage 10M Production Rehearsal Tracker', 'GARMETIX-10M-20260620-4131'])
    and "APP_VERSION = '4.10.31'" in app_version
    and '<Version>4.10.31</Version>' in csproj
) or (
    all(token in app_info for token in ['Version = "4.11.0"', 'Stage 11A MAUI Android Attendance Kiosk Shell', 'GARMETIX-11A-20260621-4110'])
    and "APP_VERSION = '4.11.0'" in app_version
    and '<Version>4.11.0</Version>' in csproj
) or (
    all(token in app_info for token in ['Version = "4.11.1"', 'Stage 11A Android Build Hardening', 'GARMETIX-11A-20260621-4111'])
    and "APP_VERSION = '4.11.1'" in app_version
    and '<Version>4.11.1</Version>' in csproj
) or (
    all(token in app_info for token in ['Version = "4.11.5"', 'Stage 11B-3 External Fingerprint Bridge Connector', 'GARMETIX-11B-20260621-4115'])
    and "APP_VERSION = '4.11.5'" in app_version
    and '<Version>4.11.5</Version>' in csproj
)
add('version identity', version_identity)
add('new master modules', all(token in import_export for token in ['["products"]', '["customers"]', '["vendors"]', '["stock-opening"]']))
add('new import methods', all(token in import_export for token in ['ImportProductsAsync', 'ImportCustomersAsync', 'ImportVendorsAsync', 'ImportStockOpeningAsync']))
add('stock opening posts ledger adjustment', all(token in import_export for token in ['StockOpeningImportAdjustment', 'OpeningQty', 'stockLedger.PostAsync', 'allowNegative: true']))
add('customer vendor validation included', all(token in import_export for token in ['CreditBalance', 'LoyaltyPoints', 'PAN', 'TAN', 'ResolveCompanyFromList']))
add('center counts updated', all(token in import_export for token in ['counts["customers"]', 'counts["vendors"]', 'counts["stocks"]', 'Definitions.Keys.Count(IsImportSupported)']))
add('frontend knows new modules', all(token in import_page for token in ['products:', 'customers:', 'vendors:', "'stock-opening':", 'Real import engine enabled']))
add('dashboard single scroll guardrails', all(token in app_shell for token in ['garmetix-dashboard-shell', 'garmetix-dashboard-panel-body']) and 'garmetix-dashboard-panel-body' in legacy_shell and all(token in main_css for token in ['body.garmetix-dashboard-shell', 'height: 100dvh', 'overflow: hidden', '.garmetix-dashboard-panel-body', 'scrollbar-gutter: stable', 'overflow-x: auto', 'overflow-y: visible']))
add('current release invokes stage10j', 'stage10j-import-export-engine-check.py' in current_release)
add('host drill exists', all(token in drill for token in ['products customers vendors stock-opening attendance', 'import-export/template/$module', 'stage10j import/export engine']))

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(('PASS' if ok else 'FAIL') + f': {name}')
if failed:
    raise SystemExit('Stage 10J import/export engine validation failed: ' + ', '.join(failed))
print('Stage 10J import/export engine validation passed.')

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
)
add('version identity', version_identity)
add('new master modules', all(token in import_export for token in ['["products"]', '["customers"]', '["vendors"]', '["stock-opening"]']))
add('new import methods', all(token in import_export for token in ['ImportProductsAsync', 'ImportCustomersAsync', 'ImportVendorsAsync', 'ImportStockOpeningAsync']))
add('stock opening posts ledger adjustment', all(token in import_export for token in ['StockOpeningImportAdjustment', 'OpeningQty', 'stockLedger.PostAsync', 'allowNegative: true']))
add('customer vendor validation included', all(token in import_export for token in ['CreditBalance', 'LoyaltyPoints', 'PAN', 'TAN', 'ResolveCompanyFromList']))
add('center counts updated', all(token in import_export for token in ['counts["customers"]', 'counts["vendors"]', 'counts["stocks"]', 'Definitions.Keys.Count(IsImportSupported)']))
add('frontend knows new modules', all(token in import_page for token in ['products:', 'customers:', 'vendors:', "'stock-opening':", 'Real import engine enabled']))
add('current release invokes stage10j', 'stage10j-import-export-engine-check.py' in current_release)
add('host drill exists', all(token in drill for token in ['products customers vendors stock-opening attendance', 'import-export/template/$module', 'stage10j import/export engine']))

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(('PASS' if ok else 'FAIL') + f': {name}')
if failed:
    raise SystemExit('Stage 10J import/export engine validation failed: ' + ', '.join(failed))
print('Stage 10J import/export engine validation passed.')

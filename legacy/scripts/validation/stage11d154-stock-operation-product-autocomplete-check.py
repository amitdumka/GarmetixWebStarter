from pathlib import Path

root = Path(__file__).resolve().parents[2]
page = root / 'frontend/garmetix-web/pages/stock-operations/index.vue'
app = root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs'
front = root / 'frontend/garmetix-web/utils/appVersion.ts'
csproj = root / 'backend/Garmetix.Api/Garmetix.Api.csproj'

checks = []

def add(name, ok):
    checks.append((name, bool(ok)))

text = page.read_text()
add('stock operations page exists', page.exists())
add('version backend updated', '4.12.69' in app.read_text() and 'GARMETIX-11D154-20260703-4269' in app.read_text())
add('version frontend updated', '4.12.69' in front.read_text() and 'GARMETIX-11D154-20260703-4269' in front.read_text())
add('csproj updated', '4.12.69' in csproj.read_text() and 'stage11d154-stock-operation-product-autocomplete' in csproj.read_text())
add('autocomplete search input added', 'stockProductSearchInput' in text)
add('four product USelectMenu instances', text.count('<USelectMenu') >= 4)
add('adjustment uses autocomplete', 'v-model="adjustmentForm.stockId"' in text and 'Search product / barcode / store' in text)
add('transfer uses autocomplete', 'v-model="transferForm.fromStockId"' in text and 'Search source product / barcode / store' in text)
add('physical count uses autocomplete', 'v-model="countForm.stockId"' in text and 'Search counted product / barcode / store' in text)
add('write-off uses autocomplete', 'v-model="writeOffForm.stockId"' in text and 'Search damaged/unusable product' in text)
add('selected stock hint added', 'stockSelectionText' in text and 'Barcode ${stock.barcode' in text)
add('docs exist', (root / 'docs/stages/stage-11/Stage11D154-Stock-Operation-Product-Autocomplete-v4.12.69.md').exists())
add('release note exists', (root / 'RELEASE-v4.12.69.txt').exists())

failed = [name for name, ok in checks if not ok]
if failed:
    print('Stage 11D-154 validation failed:')
    for name in failed:
        print(' -', name)
    raise SystemExit(1)

print(f'All {len(checks)} Stage 11D-154 validation checks passed.')

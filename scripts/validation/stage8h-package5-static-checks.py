from pathlib import Path
root = Path(__file__).resolve().parents[2]
files = [
    root / 'backend/Garmetix.Domain/Generated/Models/Inventory/Tailoring.cs',
    root / 'backend/Garmetix.Infrastructure/Data/GarmetixDbContext.cs',
    root / 'backend/Garmetix.Api/Tailoring/TailoringDtos.cs',
    root / 'backend/Garmetix.Api/Tailoring/TailoringEndpoints.cs',
    root / 'backend/Garmetix.Api/Database/DatabaseSchemaRepairService.cs',
    root / 'frontend/garmetix-web/pages/tailoring/index.vue',
    root / 'frontend/garmetix-web/server/api/_nuxt_icon/lucide.json.get.ts',
]
missing = [str(p.relative_to(root)) for p in files if not p.exists()]
if missing:
    raise SystemExit(f'Missing files: {missing}')
model = files[0].read_text()
if 'class TailoringVendorServiceRate' not in model or 'VendorRate' not in model:
    raise SystemExit('TailoringVendorServiceRate model missing')
db = files[1].read_text()
if 'DbSet<TailoringVendorServiceRate>' not in db or 'TailoringVendorServiceRates' not in db:
    raise SystemExit('TailoringVendorServiceRate DbSet/index missing')
dtos = files[2].read_text()
for token in ['TailoringVendorRequest', 'TailoringVendorRateRequest', 'TailoringVendorRateDto']:
    if token not in dtos:
        raise SystemExit(f'{token} missing')
endpoints = files[3].read_text()
for token in ['MapGet("/vendor-rates"', 'SaveTailoringVendorAsync', 'SaveVendorRateAsync', 'BuildVendorRateDtosAsync']:
    if token not in endpoints:
        raise SystemExit(f'{token} missing')
repair = files[4].read_text()
if 'CREATE TABLE IF NOT EXISTS "TailoringVendorServiceRates"' not in repair:
    raise SystemExit('Schema repair missing TailoringVendorServiceRates')
ui = files[5].read_text()
for token in ['Vendors & Rates', 'vendorRates', 'saveTailoringVendor', 'saveVendorRate', 'applyVendorRateToLine']:
    if token not in ui:
        raise SystemExit(f'Tailoring UI missing {token}')
icons = files[6].read_text()
for token in ['normalizeIconName', "trimmed.includes(':')", 'fallback']:
    if token not in icons:
        raise SystemExit(f'Icon endpoint missing {token}')
version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
for token in ["APP_VERSION = '4.7.4'", 'Tailoring Vendor Rates and Icon Reliability', 'GARMETIX-8H-20260617-4740']:
    if token not in version:
        raise SystemExit(f'Version token missing: {token}')
print('Stage 8H Package 5 static validation passed.')

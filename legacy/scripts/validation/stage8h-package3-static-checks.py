from pathlib import Path
root = Path(__file__).resolve().parents[2]

commercial = (root / 'frontend/garmetix-web/components/CommercialNoteEntryForm.vue').read_text()
purchase = (root / 'frontend/garmetix-web/pages/purchase/new.vue').read_text()
cash = (root / 'frontend/garmetix-web/pages/cash-vouchers/index.vue').read_text()
vendor = (root / 'frontend/garmetix-web/pages/vendor-payments/index.vue').read_text()
frontend_version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
backend_version = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()

if "value: ''" in commercial or "selectedPartyId: ''" in commercial:
    raise SystemExit('Commercial note form still uses empty selector sentinel')
if "value: ''" in purchase or "vendorId: ''" in purchase:
    raise SystemExit('New purchase page still uses empty vendor selector sentinel')
if 'safeList' not in cash or 'safeGetArray' not in cash:
    raise SystemExit('Cash voucher refresh is not resilient')
if 'UiFormSlideover' not in vendor or 'formOpen' not in vendor or 'Add New Vendor Payment' not in vendor:
    raise SystemExit('Vendor payments does not use slideover add flow')
for token in ["APP_VERSION = '4.7.2'", 'Stage 8H Package 3 Purchase and Voucher Crash Hotfix', 'GARMETIX-8H-20260617-4720']:
    if token not in frontend_version:
        raise SystemExit(f'Missing frontend version token: {token}')
for token in ['Version = "4.7.2"', 'Stage 8H Package 3 Purchase and Voucher Crash Hotfix', 'GARMETIX-8H-20260617-4720']:
    if token not in backend_version:
        raise SystemExit(f'Missing backend version token: {token}')
print('Stage 8H Package 3 static validation passed.')

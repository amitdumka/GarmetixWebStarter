from pathlib import Path
root = Path(__file__).resolve().parents[2]
new_purchase = (root / 'frontend/garmetix-web/pages/purchase/new.vue').read_text()
numbering = (root / 'backend/Garmetix.Api/Numbering/DocumentNumberService.cs').read_text()
purchase = (root / 'backend/Garmetix.Api/Purchase/PurchaseEndpoints.cs').read_text()
version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
backend = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
checks = {
    'new purchase page has product lookup': 'product-lookup' in new_purchase and 'searchProducts' in new_purchase,
    'new purchase page has inline product dialog': 'Add Product While Purchasing' in new_purchase and 'setup/quick-product' in new_purchase,
    'new purchase page has no manual inward payload': 'inwardNumber: null' in new_purchase and 'inwardNumberPreview' in new_purchase,
    'new purchase uses safe select sentinel': "const NONE = '__none__'" in new_purchase and "{ value: NONE" in new_purchase,
    'purchase inward number is monthly store code format': 'NextStoreMonthlyAsync(companyId, storeGroupId, storeId, "PurchaseInward", "INW"' in numbering,
    'purchase endpoint always generates inward number': 'var inwardNumber = await documentNumbers.NextPurchaseInwardAsync' in purchase and 'request.InwardNumber.Trim()' not in purchase,
    'frontend version updated': "APP_VERSION = '4.7.5'" in version and 'Purchase Inward Pro' in version,
    'backend version updated': 'Version = "4.7.5"' in backend and 'Purchase Inward Pro' in backend,
}
failed = [name for name, ok in checks.items() if not ok]
for name, ok in checks.items():
    print(f"[{'PASS' if ok else 'FAIL'}] {name}")
if failed:
    raise SystemExit(f"Stage 8H Package 6 checks failed: {failed}")
print('Stage 8H Package 6 static validation passed.')

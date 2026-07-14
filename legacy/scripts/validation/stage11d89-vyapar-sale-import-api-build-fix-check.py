#!/usr/bin/env python3
from pathlib import Path
checks = {
    'backend/Garmetix.Api/SaleImport/VyaparSaleImportService.cs': [
        'using InventoryProductCategory = Garmetix.Core.Models.Inventory.ProductCategory;',
        'private async Task<InventoryProductCategory> GetOrCreateCategoryAsync',
        'new InventoryProductCategory {'
    ],
    'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs': ['4.12.04', 'Stage 11D-89 Vyapar Sale Import API Build Fix', 'GARMETIX-11D89-20260628-4204'],
    'frontend/garmetix-web/utils/appVersion.ts': ["APP_VERSION = '4.12.04'", 'Stage 11D-89 Vyapar Sale Import API Build Fix'],
    'frontend/garmetix-web/package.json': ['"version": "4.12.04"'],
    'backend/Garmetix.Api/Garmetix.Api.csproj': ['<Version>4.12.04</Version>', '4.12.04-vyapar-sale-import-api-build-fix'],
    'docs/stages/stage-11/Stage11D89-Vyapar-Sale-Import-API-Build-Fix-v4.12.04.md': ['CS0104', 'ProductCategory', 'InventoryProductCategory'],
}
failed = False
for file, tokens in checks.items():
    path = Path(file)
    if not path.exists():
        print(f'MISSING FILE: {file}')
        failed = True
        continue
    text = path.read_text(errors='ignore')
    for token in tokens:
        if token not in text:
            print(f'MISSING TOKEN in {file}: {token}')
            failed = True
if failed:
    print('Stage 11D-89 validation FAILED')
    raise SystemExit(1)
print('Stage 11D-89 validation passed')

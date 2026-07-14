#!/usr/bin/env python3
from pathlib import Path
checks = {
    'backend/Garmetix.Api/SaleImport/VyaparSaleImportDtos.cs': ['VyaparSaleImportPreviewDto', 'VyaparSaleImportConfirmRequest'],
    'backend/Garmetix.Api/SaleImport/VyaparSaleImportService.cs': ['FromXlsx', 'ParseSaleReport', 'ParseItemDetails', 'VyaparSaleImportOut', 'CreateImportedProductAndStockAsync'],
    'backend/Garmetix.Api/SaleImport/VyaparSaleImportEndpoints.cs': ['/api/sale-import/vyapar', '/preview', '/confirm'],
    'backend/Garmetix.Api/Program.cs': ['using Garmetix.Api.SaleImport;', 'AddScoped<VyaparSaleImportService>', 'MapVyaparSaleImportEndpoints'],
    'frontend/garmetix-web/pages/billing/vyapar-import.vue': ['Vyapar Sale Import', 'Preview Data', 'Confirm Import', 'Export Missing Barcode CSV'],
    'frontend/garmetix-web/components/AppShell.vue': ['Vyapar Sale Import', '/billing/vyapar-import'],
    'frontend/garmetix-web/components/AppShellLegacy.vue': ['Vyapar Sale Import', '/billing/vyapar-import'],
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
    print('Stage 11D-88 feature validation FAILED')
    raise SystemExit(1)
print('Stage 11D-88 feature validation passed')

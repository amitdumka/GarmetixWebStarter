from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = {
    'Non-GST print endpoint': ('backend/Garmetix.Api/NonGstGoods/NonGstGoodsEndpoints.cs', 'MapGet("/documents/{id:guid}/print", PrintAsync)'),
    'Gross profit report field': ('backend/Garmetix.Api/NonGstGoods/NonGstGoodsDtos.cs', 'decimal GrossProfit'),
    'Current stock rows report field': ('backend/Garmetix.Api/NonGstGoods/NonGstGoodsDtos.cs', 'IReadOnlyList<NonGstReportStockRowDto> CurrentStockRows'),
    'Cost snapshot model field': ('backend/Garmetix.Domain/Generated/Models/Inventory/Inventory.cs', 'public decimal CostAmount'),
    'Tax snapshot model field': ('backend/Garmetix.Domain/Generated/Models/Inventory/Inventory.cs', 'public decimal TaxAmount'),
    'Enhancement migration': ('backend/Garmetix.Infrastructure/Data/Migrations/20260610125000_EnhanceNonGstGoodsMemoReports.cs', 'EnhanceNonGstGoodsMemoReports'),
    'Frontend sale cash memo': ('frontend/garmetix-web/pages/non-gst-goods/index.vue', 'Post & Print Cash Memo'),
    'Frontend purchase memo': ('frontend/garmetix-web/pages/non-gst-goods/index.vue', 'Post & Print Purchase Memo'),
    'Frontend current stock report': ('frontend/garmetix-web/pages/non-gst-goods/index.vue', 'Current Non-GST Stock'),
    'Version 2.4.0': ('frontend/garmetix-web/utils/appVersion.ts', "APP_VERSION = '2.4.0'"),
}

failed = []
for name, (rel, text) in checks.items():
    path = root / rel
    if not path.exists() or text not in path.read_text():
        failed.append(f'{name}: missing {text} in {rel}')

for rel in ['backend/Garmetix.Api/NonGstGoods/NonGstGoodsEndpoints.cs', 'frontend/garmetix-web/pages/non-gst-goods/index.vue']:
    text = (root / rel).read_text()
    for a, b in [('{', '}'), ('(', ')'), ('[', ']')]:
        if text.count(a) != text.count(b):
            failed.append(f'{rel}: unbalanced {a}{b}')

if failed:
    print('Stage 6E static validation failed:')
    for item in failed:
        print('-', item)
    raise SystemExit(1)

print('Stage 6E static validation passed.')

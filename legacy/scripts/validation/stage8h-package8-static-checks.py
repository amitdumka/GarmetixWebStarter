from pathlib import Path
root = Path(__file__).resolve().parents[2]
endpoint = (root / 'backend/Garmetix.Api/Inventory/StockReportEndpoints.cs').read_text()
for token in ['MapGet("/movement-history"', 'MovementHistoryAsync', 'StockMovementHistorySummaryDto', 'ProfitOrLoss', 'BuildInvoiceLineLookupAsync']:
    if token not in endpoint:
        raise SystemExit(f'Missing stock movement endpoint token: {token}')
dtos = (root / 'backend/Garmetix.Api/Inventory/StockReportDtos.cs').read_text()
for token in ['StockMovementHistorySummaryDto', 'CurrentStockValue', 'GrossProfitOrLoss', 'StockMovementHistoryRowDto']:
    if token not in dtos:
        raise SystemExit(f'Missing stock movement DTO token: {token}')
page = (root / 'frontend/garmetix-web/pages/stock-reports/index.vue').read_text()
for token in ['loadMovementHistory', 'stock-movement-card', 'Profit / Loss', 'Current Stock Value', 'Export history']:
    if token not in page:
        raise SystemExit(f'Missing stock report UI token: {token}')
version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
for token in ["APP_VERSION = '4.7.7'", 'Stage 8H Package 8 Stock Movement Profit History', 'GARMETIX-8H-20260617-4770']:
    if token not in version:
        raise SystemExit(f'Missing frontend version token: {token}')
backend = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
for token in ['Version = "4.7.7"', 'Stage 8H Package 8 Stock Movement Profit History', 'GARMETIX-8H-20260617-4770']:
    if token not in backend:
        raise SystemExit(f'Missing backend version token: {token}')
for rel in [
    'docs/stages/stage-8/Stage8H-Package8-StockMovementProfitHistory-v4.7.7-Notes.md',
    'docs/operations/Stock-Movement-Profit-History-v4.7.7.md'
]:
    if not (root / rel).exists():
        raise SystemExit(f'Missing doc: {rel}')
print('Stage 8H Package 8 static validation passed.')

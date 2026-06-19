
from pathlib import Path
root = Path(__file__).resolve().parents[2]
repair = (root / 'backend/Garmetix.Api/Database/DatabaseSchemaRepairService.cs').read_text()
for token in [
    'RepairCashVoucherConversionStorageAsync',
    'CREATE TABLE IF NOT EXISTS "CashVoucherConversions"',
    'IX_CashVoucherConversions_CompanyId_CashVoucherId',
    'await RepairCashVoucherConversionStorageAsync(db, logger, cancellationToken);',
]:
    if token not in repair:
        raise SystemExit(f'Missing cash voucher conversion repair token: {token}')

migration = root / 'backend/Garmetix.Infrastructure/Data/Migrations/20260618073000_AddCashVoucherConversionsTable.cs'
if not migration.exists():
    raise SystemExit('Missing CashVoucherConversions migration.')
mt = migration.read_text()
for token in ['AddCashVoucherConversionsTable', 'CREATE TABLE IF NOT EXISTS "CashVoucherConversions"', 'DropTable(name: "CashVoucherConversions")']:
    if token not in mt:
        raise SystemExit(f'Missing migration token: {token}')

version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
for token in ["APP_VERSION = '4.8.0'", 'Stage 8H Package 11 Voucher Edit Delete Repair', 'GARMETIX-8H-20260618-4800']:
    if token not in version:
        raise SystemExit(f'Missing frontend version token: {token}')
backend = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
for token in ['Version = "4.8.0"', 'Stage = "Stage 8H Package 11 Voucher Edit Delete Repair"', 'GARMETIX-8H-20260618-4800']:
    if token not in backend:
        raise SystemExit(f'Missing backend version token: {token}')
print('Stage 8H Package 11 voucher edit/delete static validation passed.')

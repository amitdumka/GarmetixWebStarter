from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[2]
errors: list[str] = []

migration = ROOT / 'backend/Garmetix.Infrastructure/Data/Migrations/20260609173500_ConsolidateStage3To5Schema.cs'
if not migration.exists():
    errors.append('Missing consolidated Stage 5E migration file.')
else:
    text = migration.read_text(encoding='utf-8')
    required_tokens = [
        'ConsolidateStage3To5Schema',
        'CREATE TABLE IF NOT EXISTS "PurchasePayments"',
        'CREATE TABLE IF NOT EXISTS "StockMovements"',
        'CREATE TABLE IF NOT EXISTS "DocumentSequences"',
        'ALTER TABLE IF EXISTS "PurchaseInvoices" ADD COLUMN IF NOT EXISTS "StoreId"',
        'ALTER TABLE IF EXISTS "InvoiceItems" ADD COLUMN IF NOT EXISTS "HSNCode"',
        'ALTER TABLE IF EXISTS "PurchaseInvoiceItems" ADD COLUMN IF NOT EXISTS "CGSTAmount"',
        'CREATE INDEX IF NOT EXISTS "IX_DocumentSequences_Company_Store_Type_Date"',
        'No-op by design',
    ]
    for token in required_tokens:
        if token not in text:
            errors.append(f'Missing migration token: {token}')

program = (ROOT / 'backend/Garmetix.Api/Program.cs').read_text(encoding='utf-8')
if 'app.MapDatabaseMigrationEndpoints();' not in program:
    errors.append('Program.cs does not map database migration status endpoints.')

endpoint = ROOT / 'backend/Garmetix.Api/Database/DatabaseMigrationEndpoints.cs'
if not endpoint.exists():
    errors.append('Missing DatabaseMigrationEndpoints.cs.')
else:
    endpoint_text = endpoint.read_text(encoding='utf-8')
    for token in ['GetAppliedMigrationsAsync', 'GetPendingMigrationsAsync', '/api/database/migrations', '20260609173500_ConsolidateStage3To5Schema']:
        if token not in endpoint_text:
            errors.append(f'Missing database endpoint token: {token}')

di = (ROOT / 'backend/Garmetix.Infrastructure/DependencyInjection.cs').read_text(encoding='utf-8')
if 'warnings.Ignore(RelationalEventId.PendingModelChangesWarning)' not in di:
    errors.append('PendingModelChangesWarning is not configured for stable runtime migration handling.')

# Simple C# brace balance check for new files.
for file in [migration, endpoint]:
    if file.exists():
        text = file.read_text(encoding='utf-8')
        if text.count('{') != text.count('}'):
            errors.append(f'Brace imbalance in {file.relative_to(ROOT)}')

if errors:
    for error in errors:
        print(f'ERROR: {error}')
    sys.exit(1)

print('Stage 5E static checks passed.')

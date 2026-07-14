from pathlib import Path
root = Path(__file__).resolve().parents[2]

checks = {
    'backend/Garmetix.Api/StoreDay/StoreDayEndpoints.cs': [
        'MapStoreDayEndpoints',
        '/status',
        '/open',
        '/close',
        '/holiday',
        'CashDetailDto',
        'UpsertPettyCashSheetAsync',
        'CalculateBookSummaryAsync',
        'StoreHoliday',
        'Day opening is required before day closing',
    ],
    'backend/Garmetix.Api/StoreDay/StoreDayGuardMiddleware.cs': [
        'StoreDayGuardMiddleware',
        'IsStoreManagerOrBiller',
        'IsAccountant',
        'Store day opening is required before daily entries',
        'Store day is already closed',
        'LoginRole.StoreManager',
        'LoginRole.Salesman',
        'UserType.Sales',
    ],
    'backend/Garmetix.Infrastructure/Data/GarmetixDbContext.cs': [
        'public DbSet<CashDetail> CashDetails',
        'public DbSet<DayBegin> DayBegins',
        'public DbSet<DayEnd> DayEnds',
    ],
    'backend/Garmetix.Api/Program.cs': [
        'using Garmetix.Api.StoreDay;',
        'app.UseMiddleware<StoreDayGuardMiddleware>();',
        'app.MapStoreDayEndpoints();',
    ],
    'backend/Garmetix.Api/Database/DatabaseSchemaRepairService.cs': [
        'RepairStoreDayStorageAsync',
        'CREATE TABLE IF NOT EXISTS "CashDetails"',
        'CREATE TABLE IF NOT EXISTS "DayBegins"',
        'CREATE TABLE IF NOT EXISTS "DayEnds"',
        'CREATE TABLE IF NOT EXISTS "PettyCashSheets"',
    ],
    'frontend/garmetix-web/pages/store-day/index.vue': [
        'Store Day Open / Close',
        'store-day/status',
        'store-day/open',
        'store-day/close',
        'store-day/holiday',
        'Close Day + Save Petty Cash',
        'Mark Holiday / Closed',
    ],
    'frontend/garmetix-web/components/AppShell.vue': [
        '/store-day',
        'Store Day Open / Close',
        'day opening',
        'day closing',
    ],
    'docs/planning/TODO.md': [
        'Completed in v4.9.8',
        'mandatory day-open/day-close guard',
    ],
}

for rel, tokens in checks.items():
    text = (root / rel).read_text()
    for token in tokens:
        if token not in text:
            raise SystemExit(f'Missing {token} in {rel}')

version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
for token in ["APP_VERSION = '4.9.8'", 'Stage 8I Package 9 Store Day Book', 'GARMETIX-8I-20260618-4980']:
    if token not in version:
        raise SystemExit(f'Missing frontend version token: {token}')

backend = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
for token in ['Version = "4.9.8"', 'Stage 8I Package 9 Store Day Book', 'GARMETIX-8I-20260618-4980']:
    if token not in backend:
        raise SystemExit(f'Missing backend version token: {token}')

for rel in [
    'docs/stages/stage-8/Stage8I-Package9-StoreDayBook-v4.9.8-Notes.md',
    'docs/operations/Store-Day-Opening-Closing-v4.9.8.md',
]:
    if not (root / rel).exists():
        raise SystemExit(f'Missing doc: {rel}')

print('Stage 8I Package 9 static validation passed.')

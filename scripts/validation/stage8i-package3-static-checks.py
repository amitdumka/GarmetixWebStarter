from pathlib import Path
root = Path(__file__).resolve().parents[2]

checks = {
    'backend/Garmetix.Api/Seeds/PortableSeederEndpoints.cs': [
        'MapPortableSeederEndpoints',
        'MapGet("/export"',
        'PortableSeederFileDto',
        'jsonb_populate_record',
        'GarmetixPortableJsonSeeder',
    ],
    'backend/Garmetix.Api/Seeds/AfssSeederDtos.cs': [
        'Guid? CompanyId',
        'bool CreateNewCompany = false',
    ],
    'backend/Garmetix.Api/Seeds/AfssDefaultSeederService.cs': [
        'ResolveSeedCompanyAsync',
        'CreateNewCompany',
        'SeedAccountingDefaultsForCompanyAsync',
        'AccountingDefaultProtection.CreatedByMarker',
        'companyCode',
    ],
    'backend/Garmetix.Api/Accounting/AccountingDefaultProtection.cs': [
        'SystemDefaultAccounting',
        'ProtectedLedgerGroupNames',
        'ProtectedLedgerNames',
        'IsProtectedLedgerGroup',
        'IsProtectedLedger',
    ],
    'backend/Garmetix.Api/Program.cs': [
        'app.MapPortableSeederEndpoints();',
        'SeedAccountingDefaultsForCompanyAsync',
        'Default Indian accounting ledger groups are protected',
        'Default Indian accounting ledgers are protected',
        'ApplyCascadeSoftDeleteAsync',
        'SoftDeleteByScopeColumnAsync',
    ],
    'frontend/garmetix-web/pages/af-ss/index.vue': [
        'createNewCompany',
        'portable-seeder/export',
        'portable-seeder/import',
        'Create company/store group/store automatically',
        'Create seeder file from current data',
    ],
}
for rel, tokens in checks.items():
    text = (root / rel).read_text()
    for token in tokens:
        if token not in text:
            raise SystemExit(f'Missing {token} in {rel}')

version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
for token in ["APP_VERSION = '4.9.2'", 'Stage 8I Package 3 Portable JSON Seeder', 'GARMETIX-8I-20260618-4920']:
    if token not in version:
        raise SystemExit(f'Missing frontend version token: {token}')

backend = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
for token in ['Version = "4.9.2"', 'Stage 8I Package 3 Portable JSON Seeder', 'GARMETIX-8I-20260618-4920']:
    if token not in backend:
        raise SystemExit(f'Missing backend version token: {token}')

for rel in [
    'docs/stages/stage-8/Stage8I-Package3-PortableJsonSeeder-v4.9.2-Notes.md',
    'docs/operations/Portable-Json-Seeder-v4.9.2.md',
    'docs/planning/TODO.md',
]:
    if not (root / rel).exists():
        raise SystemExit(f'Missing doc: {rel}')

print('Stage 8I Package 3 static validation passed.')

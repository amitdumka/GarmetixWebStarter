from pathlib import Path
root = Path(__file__).resolve().parents[2]

portable = (root / 'backend/Garmetix.Api/Seeds/PortableSeederEndpoints.cs').read_text()
for token in [
    'using System.Text.Json.Nodes;',
    'private static bool TryGetGuid',
    'private static async Task<string> NormalizeLedgerGroupReferenceAsync',
    'private static async Task SeedDefaultsForAllCompaniesAsync',
    '.Select(column => column!)',
]:
    if token not in portable:
        raise SystemExit(f'Missing portable seeder compile fix token: {token}')

for rel in [
    'backend/Garmetix.Api/Seeds/CompanyMergeEndpoints.cs',
    'backend/Garmetix.Api/Seeds/AfssDefaultSeederService.cs',
]:
    text = (root / rel).read_text()
    for bad in [
        'CreatedBy = "CompanyMerge"',
        'CreatedBy = "AFSSSeeder"',
    ]:
        if bad in text:
            raise SystemExit(f'Invalid setup model CreatedBy assignment remains in {rel}: {bad}')

service = (root / 'backend/Garmetix.Api/Seeds/AfssDefaultSeederService.cs').read_text()
if '"Smart Menswear profile merges into Aadwika Fashion company under Aadwika Fashion MBO store group.",' not in service:
    raise SystemExit('Seeder2CsOnly comma hotfix is missing.')

version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
for token in ["APP_VERSION = '4.9.6'", 'Stage 8I Package 7 Seeder Compile Hotfix', 'GARMETIX-8I-20260618-4960']:
    if token not in version:
        raise SystemExit(f'Missing frontend version token: {token}')

backend = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
for token in ['Version = "4.9.6"', 'Stage 8I Package 7 Seeder Compile Hotfix', 'GARMETIX-8I-20260618-4960']:
    if token not in backend:
        raise SystemExit(f'Missing backend version token: {token}')

for rel in [
    'docs/stages/stage-8/Stage8I-Package7-CompileHotfix-v4.9.6-Notes.md',
    'docs/operations/Seeder-Compile-Hotfix-v4.9.6.md',
]:
    if not (root / rel).exists():
        raise SystemExit(f'Missing doc: {rel}')

print('Stage 8I Package 7 static validation passed.')

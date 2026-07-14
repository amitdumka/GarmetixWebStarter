from pathlib import Path
root = Path(__file__).resolve().parents[2]

checks = {
    'backend/Garmetix.Api/Seeds/AfssDefaultSeederService.cs': [
        'Smart Menswear',
        '"AF"',
        '"MBO"',
        '"Aadwika Fashion MBO"',
        '"Aadwika Fashion - Shalini"',
        'IsAadwikaAmitOrSmartProfile',
        'This web seeder merges Aadwika Fashion Amit Kumar and Smart Menswear',
    ],
    'backend/Garmetix.Api/Seeds/PortableSeederEndpoints.cs': [
        'IsProtectedDefaultAccountingRow',
        'skippedDefaultAccountingRows',
        'protectedLedgerGroupByOldId',
        'NormalizeLedgerGroupReferenceAsync',
        'SeedDefaultsForAllCompaniesAsync',
        'system defaults won the clash',
    ],
    'backend/Garmetix.Api/Accounting/AccountingDefaultProtection.cs': [
        'ProtectedLedgerGroupNames',
        'ProtectedLedgerNames',
        'SystemDefaultAccounting',
    ],
    'docs/planning/TODO.md': [
        'Completed in v4.9.3',
        'Default Indian accounting ledger groups/ledgers win',
    ],
}
for rel, tokens in checks.items():
    text = (root / rel).read_text()
    for token in tokens:
        if token not in text:
            raise SystemExit(f'Missing {token} in {rel}')

service = (root / 'backend/Garmetix.Api/Seeds/AfssDefaultSeederService.cs').read_text()
if 'Samrat' in service:
    raise SystemExit('Old Samrat wording still exists in AFSS seeder service.')
if '"Smart Menswear - under Aadwika Fashion"' not in service:
    raise SystemExit('Smart Menswear merged profile label missing.')

version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
for token in ["APP_VERSION = '4.9.3'", 'Stage 8I Package 4 Seeder Merge Defaults', 'GARMETIX-8I-20260618-4930']:
    if token not in version:
        raise SystemExit(f'Missing frontend version token: {token}')

backend = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
for token in ['Version = "4.9.3"', 'Stage 8I Package 4 Seeder Merge Defaults', 'GARMETIX-8I-20260618-4930']:
    if token not in backend:
        raise SystemExit(f'Missing backend version token: {token}')

for rel in [
    'docs/stages/stage-8/Stage8I-Package4-SeederMergeDefaults-v4.9.3-Notes.md',
    'docs/operations/Seeder-Merge-Defaults-v4.9.3.md',
]:
    if not (root / rel).exists():
        raise SystemExit(f'Missing doc: {rel}')

print('Stage 8I Package 4 static validation passed.')

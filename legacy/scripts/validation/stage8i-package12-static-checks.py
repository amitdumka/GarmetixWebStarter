from pathlib import Path
root = Path(__file__).resolve().parents[2]

checks = {
    'backend/Garmetix.Api/StoreDay/CashDetailsEndpoints.cs': [
        'MapCashDetailsEndpoints',
        'CashDetailSaveRequest',
        'CashDetailHistoryResponse',
        'group.MapGet("/history"',
        'group.MapPost("/", CreateAsync)',
        'group.MapPut("/{id:guid}", UpdateAsync)',
        'group.MapDelete("/{id:guid}", DeleteAsync)',
        'ManualCashFlow',
        'SyncLinkedDayRecordsAsync',
        'This cash detail is linked to Day Opening/Closing',
    ],
    'backend/Garmetix.Api/Program.cs': [
        'app.MapCashDetailsEndpoints();',
    ],
    'frontend/garmetix-web/pages/cash-details/index.vue': [
        'Cash Details',
        'cash-details/history',
        'Add Manual Cash Detail',
        'Cash Notes / Coin History',
        'ManualCashFlow',
        'linkedToDayOpening',
        'linkedToDayClosing',
    ],
    'frontend/garmetix-web/components/AppShell.vue': [
        '/cash-details',
        'Cash Details',
        'cash notes',
        'coin history',
    ],
    'docs/planning/TODO.md': [
        'Completed in v4.9.11',
        'Cash Details register page',
    ],
}

for rel, tokens in checks.items():
    text = (root / rel).read_text()
    for token in tokens:
        if token not in text:
            raise SystemExit(f'Missing {token} in {rel}')

version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
for token in ["APP_VERSION = '4.9.11'", 'Stage 8I Package 12 Cash Details Register', 'GARMETIX-8I-20260619-49110']:
    if token not in version:
        raise SystemExit(f'Missing frontend version token: {token}')

backend = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
for token in ['Version = "4.9.11"', 'Stage 8I Package 12 Cash Details Register', 'GARMETIX-8I-20260619-49110']:
    if token not in backend:
        raise SystemExit(f'Missing backend version token: {token}')

for rel in [
    'docs/stages/stage-8/Stage8I-Package12-CashDetailsRegister-v4.9.11-Notes.md',
    'docs/operations/Cash-Details-Register-v4.9.11.md',
]:
    if not (root / rel).exists():
        raise SystemExit(f'Missing doc: {rel}')

print('Stage 8I Package 12 static validation passed.')

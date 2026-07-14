from pathlib import Path
root = Path(__file__).resolve().parents[2]

checks = {
    'frontend/garmetix-web/composables/useUiFeedback.ts': [
        'function success(',
        'success,',
    ],
    'frontend/garmetix-web/pages/store-day/index.vue': [
        'reopenDay',
        'deleteDayClose',
        'store-day/reopen',
        'store-day/delete-close',
        'Reopen Day',
        'Delete Close',
        "feedback.notify('Store day opened')",
    ],
    'backend/Garmetix.Api/StoreDay/StoreDayEndpoints.cs': [
        'StoreDayReopenRequest',
        'group.MapPost("/reopen"',
        'group.MapPost("/delete-close"',
        'VoidDayCloseAsync',
        'DayClosing{action}',
        'db.CashVouchers.AsNoTracking()',
    ],
    'backend/Garmetix.Api/Accounting/PettyCashEndpoints.cs': [
        'BuildTransactionDetailLinesAsync',
        'PettyCashTransactionLine',
        'db.CashVouchers.AsNoTracking()',
        'Cash Voucher {voucher.VoucherType}',
    ],
    'backend/Garmetix.Api/Accounting/PettyCashPdfDocument.cs': [
        'PettyCashTransactionLine',
        'DrawTransactionDetails',
        'BOOK TRANSACTION DETAILS',
        'BuildPdf(IReadOnlyList<string> contents)',
        'Income: INR',
        'Expense: INR',
    ],
    'backend/Garmetix.Api/Program.cs': [
        'EnsureUniqueDailyRecordAsync',
        'Attendance already exists for this employee and date.',
    ],
    'docs/planning/TODO.md': [
        'Completed in v4.9.9',
        'Petty cash auto calculation now includes Cash Voucher',
    ],
}

for rel, tokens in checks.items():
    text = (root / rel).read_text()
    for token in tokens:
        if token not in text:
            raise SystemExit(f'Missing {token} in {rel}')

frontend = (root / 'frontend/garmetix-web/pages/store-day/index.vue').read_text()
if "feedback.success('Store day opened')" in frontend or "feedback.success('Store day closed" in frontend:
    raise SystemExit('Store Day page still uses feedback.success directly.')

version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
for token in ["APP_VERSION = '4.9.9'", 'Stage 8I Package 10 Store Day Petty Cash Hotfix', 'GARMETIX-8I-20260619-4990']:
    if token not in version:
        raise SystemExit(f'Missing frontend version token: {token}')

backend = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
for token in ['Version = "4.9.9"', 'Stage 8I Package 10 Store Day Petty Cash Hotfix', 'GARMETIX-8I-20260619-4990']:
    if token not in backend:
        raise SystemExit(f'Missing backend version token: {token}')

for rel in [
    'docs/stages/stage-8/Stage8I-Package10-StoreDayPettyCashHotfix-v4.9.9-Notes.md',
    'docs/operations/Store-Day-PettyCash-Hotfix-v4.9.9.md',
]:
    if not (root / rel).exists():
        raise SystemExit(f'Missing doc: {rel}')

print('Stage 8I Package 10 static validation passed.')

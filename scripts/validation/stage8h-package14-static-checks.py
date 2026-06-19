from pathlib import Path
root = Path(__file__).resolve().parents[2]

checks = {
    'frontend/garmetix-web/pages/vouchers/index.vue': [
        'accountingDateTimeForApi(form.onDate)',
        'localDateValue()',
        'dateInputValue(voucher.onDate)',
        'parseLocalDate(value)',
    ],
    'frontend/garmetix-web/pages/cash-vouchers/index.vue': [
        'accountingDateTimeForApi(form.onDate)',
        'parseLocalDate(value)',
        'dateInputValue(value)',
    ],
    'frontend/garmetix-web/pages/petty-cash/index.vue': [
        'accountingDateTimeForApi(form.onDate)',
        'dateInputValue(sheet.onDate)',
        'parseLocalDate(value)',
    ],
}
for rel, tokens in checks.items():
    text = (root / rel).read_text()
    if 'toISOString()' in text and rel.endswith(('vouchers/index.vue', 'petty-cash/index.vue')):
        raise SystemExit(f'Unexpected toISOString still present in {rel}')
    for token in tokens:
        if token not in text:
            raise SystemExit(f'Missing token in {rel}: {token}')

backend_voucher = (root / 'backend/Garmetix.Api/Accounting/AccountingPostingService.cs').read_text()
if 'voucher.OnDate = request.OnDate.Date;' not in backend_voucher:
    raise SystemExit('Voucher backend does not normalize OnDate.Date')
backend_cash = (root / 'backend/Garmetix.Api/OffBook/CashVoucherEndpoints.cs').read_text()
if 'voucher.OnDate = request.OnDate.Date;' not in backend_cash:
    raise SystemExit('Cash voucher backend does not normalize OnDate.Date')
backend_petty = (root / 'backend/Garmetix.Api/Accounting/PettyCashEndpoints.cs').read_text()
if 'sheet.OnDate = sheet.OnDate.Date;' not in backend_petty:
    raise SystemExit('Petty cash backend date normalization missing')

version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
for token in ["APP_VERSION = '4.8.3'", 'Stage 8H Package 14 Accounting Date Hotfix', 'GARMETIX-8H-20260618-4830']:
    if token not in version:
        raise SystemExit(f'Missing frontend version token: {token}')

for rel in [
    'docs/stages/stage-8/Stage8H-Package14-AccountingDateHotfix-v4.8.3-Notes.md',
    'docs/operations/Accounting-Date-Hotfix-v4.8.3.md',
]:
    if not (root / rel).exists():
        raise SystemExit(f'Missing doc: {rel}')

print('Stage 8H Package 14 static validation passed.')

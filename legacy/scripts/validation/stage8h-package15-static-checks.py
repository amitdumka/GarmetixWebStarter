from pathlib import Path
root = Path(__file__).resolve().parents[2]

page = (root / 'frontend/garmetix-web/pages/accounting/index.vue').read_text()
for token in [
    'id: bankAccountForm.id || null',
    'Bank account edit lost the original record id',
    "await api.update<any>(endpoint, id, payload)",
    "bankAccounts: 'bank-accounts'",
]:
    if token not in page:
        raise SystemExit(f'Missing Accounting page token: {token}')

service = (root / 'backend/Garmetix.Api/Accounting/AccountingPostingService.cs').read_text()
for token in [
    'var accountNumber = request.AccountNumber.Trim();',
    'A bank account with this bank and account number already exists.',
    'account.AccountNumber = accountNumber;',
]:
    if token not in service:
        raise SystemExit(f'Missing bank duplicate safety token: {token}')

version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
for token in ["APP_VERSION = '4.8.4'", 'Stage 8H Package 15 Bank Account Edit Hotfix', 'GARMETIX-8H-20260618-4840']:
    if token not in version:
        raise SystemExit(f'Missing frontend version token: {token}')

for rel in [
    'docs/stages/stage-8/Stage8H-Package15-BankAccountEditHotfix-v4.8.4-Notes.md',
    'docs/operations/Bank-Account-Edit-Hotfix-v4.8.4.md',
]:
    if not (root / rel).exists():
        raise SystemExit(f'Missing doc: {rel}')

print('Stage 8H Package 15 static validation passed.')

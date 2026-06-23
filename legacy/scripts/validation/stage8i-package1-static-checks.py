from pathlib import Path
root = Path(__file__).resolve().parents[2]

checks = {
    'backend/Garmetix.Api/Validation/DataConsistencyEndpoints.cs': [
        'CheckProductionCleanupAsync',
        'DUPLICATE_BANK_ACCOUNT',
        'VOUCHER_DATE_TIME_COMPONENT',
        'CASH_VOUCHER_DATE_TIME_COMPONENT',
        'PETTY_CASH_DATE_TIME_COMPONENT',
        'VOUCHER_JOURNAL_MISSING',
    ],
    'frontend/garmetix-web/pages/data-consistency/index.vue': [
        'Production Cleanup Focus',
        'duplicateBankIssues',
        'wrongDateIssues',
        'missingJournalIssues',
    ],
    'frontend/garmetix-web/pages/backup-maintenance/index.vue': [
        'Production backup/restore drill',
        'RESTORE_DRILL_KEY',
        'restoreDrillSteps',
        'restoreDrillNote',
    ],
    'frontend/garmetix-web/pages/gst-final-acceptance/index.vue': [
        'GST Final Acceptance',
        'acceptanceSteps',
        'data-consistency/summary',
        'email-diagnostics/status',
        'backups/maintenance/status',
    ],
    'frontend/garmetix-web/components/AppShell.vue': [
        '/gst-final-acceptance',
        'GST Final Acceptance',
    ],
}
for rel, tokens in checks.items():
    text = (root / rel).read_text()
    for token in tokens:
        if token not in text:
            raise SystemExit(f'Missing {token} in {rel}')

version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
for token in ["APP_VERSION = '4.9.0'", 'Stage 8I Package 1 Next Three Production Parts', 'GARMETIX-8I-20260618-4900']:
    if token not in version:
        raise SystemExit(f'Missing frontend version token: {token}')

backend = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
for token in ['Version = "4.9.0"', 'Stage 8I Package 1 Next Three Production Parts', 'GARMETIX-8I-20260618-4900']:
    if token not in backend:
        raise SystemExit(f'Missing backend version token: {token}')

for rel in [
    'docs/stages/stage-8/Stage8I-Package1-NextThree-v4.9.0-Notes.md',
    'docs/operations/Stage8I-NextThree-Production-Acceptance-v4.9.0.md',
]:
    if not (root / rel).exists():
        raise SystemExit(f'Missing doc: {rel}')

print('Stage 8I Package 1 static validation passed.')

from pathlib import Path
root = Path(__file__).resolve().parents[2]
checks = [
    ('ExportActions component', root/'frontend/garmetix-web/components/dashboard/ExportActions.vue', ['exportJson', 'exportCsv', 'printDashboard']),
    ('Store dashboard export', root/'frontend/garmetix-web/pages/dashboard/store-manager/index.vue', ['DashboardExportActions', 'exportTables']),
    ('Business dashboard export', root/'frontend/garmetix-web/pages/dashboard/business/index.vue', ['DashboardExportActions', 'exportTables']),
    ('Frontend version', root/'frontend/garmetix-web/utils/appVersion.ts', ["APP_VERSION = '3.9.0'", "APP_STAGE = 'Stage 7J'", "GARMETIX-7J-20260610-390"]),
    ('Backend version', root/'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs', ['Version = "3.9.0"', 'Stage = "Stage 7J"', 'GARMETIX-7J-20260610-390']),
    ('Package version', root/'frontend/garmetix-web/package.json', ['"version": "3.9.0"']),
    ('Print CSS', root/'frontend/garmetix-web/assets/css/main.css', ['@media print', 'dashboard-v3-page'])
]
errors = []
for label, path, needles in checks:
    if not path.exists():
        errors.append(f'{label}: missing {path}')
        continue
    text = path.read_text(errors='ignore')
    for needle in needles:
        if needle not in text:
            errors.append(f'{label}: missing {needle}')
if errors:
    print('Stage 7J static validation failed:')
    for error in errors:
        print(' -', error)
    raise SystemExit(1)
print('Stage 7J static validation passed.')

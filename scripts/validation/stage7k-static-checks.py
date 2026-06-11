from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = [
    ('Frontend version', root/'frontend/garmetix-web/utils/appVersion.ts', ["APP_VERSION = '3.10.0'", "APP_STAGE = 'Stage 7K'", 'GARMETIX-7K-20260610-3100']),
    ('Backend version', root/'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs', ['Version = "3.10.0"', 'Stage = "Stage 7K"', 'GARMETIX-7K-20260610-3100']),
    ('Package version', root/'frontend/garmetix-web/package.json', ['"version": "3.10.0"']),
    ('Dashboard filter component', root/'frontend/garmetix-web/components/dashboard/FilterBar.vue', ['Dashboard filters', 'Auto refresh', 'Last refreshed']),
    ('Dashboard preferences composable', root/'frontend/garmetix-web/composables/useDashboardPreferences.ts', ['useDashboardPreferences', 'localStorage', 'toQueryParams', 'refreshIntervalSeconds']),
    ('Store manager dashboard filters', root/'frontend/garmetix-web/pages/dashboard/store-manager/index.vue', ['DashboardFilterBar', "useDashboardPreferences('store-manager-dashboard')", 'configureAutoRefresh', 'lastRefreshedAt']),
    ('Business dashboard filters', root/'frontend/garmetix-web/pages/dashboard/business/index.vue', ['DashboardFilterBar', "useDashboardPreferences('business-dashboard')", 'configureAutoRefresh', 'lastRefreshedAt']),
    ('Backend period filters', root/'backend/Garmetix.Api/Dashboard/DashboardEndpoints.cs', ['DateTime? from', 'DateTime? to', 'ResolvePeriod', 'period.FromDate', 'period.ToExclusive']),
    ('Dashboard period DTO', root/'backend/Garmetix.Api/Dashboard/DashboardDtos.cs', ['DashboardPeriodDto', 'DashboardPeriodDto Period']),
    ('Stage 7 TODO layout audit', root/'Stage7-TODO.md', ['Required UI Layout Audit', 'proper outer margin and padding', 'overlaps the sidebar', 'industry-standard layout']),
    ('Implementation map', root/'Stage7-Implementation-Map.md', ['Version: 3.10.0', 'Stage 7K Dashboard Filter Map', 'FilterBar.vue', 'useDashboardPreferences.ts'])
]

errors = []
for name, path, needles in checks:
    if not path.exists():
        errors.append(f'{name}: missing {path}')
        continue
    text = path.read_text(encoding='utf-8')
    for needle in needles:
        if needle not in text:
            errors.append(f'{name}: missing {needle!r} in {path}')

for rel in [
    'backend/Garmetix.Api/Dashboard/DashboardEndpoints.cs',
    'backend/Garmetix.Api/Dashboard/DashboardDtos.cs',
    'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs'
]:
    path = root/rel
    text = path.read_text(encoding='utf-8')
    if text.count('{') != text.count('}'):
        errors.append(f'Brace balance failed: {rel}')

if errors:
    print('Stage 7K static validation failed:')
    for error in errors:
        print('-', error)
    raise SystemExit(1)

print('Stage 7K static validation passed.')

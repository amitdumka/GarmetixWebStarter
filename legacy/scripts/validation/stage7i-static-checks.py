from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
errors = []

required = [
    'frontend/garmetix-web/components/dashboard/PageHero.vue',
    'frontend/garmetix-web/components/dashboard/MetricGrid.vue',
    'frontend/garmetix-web/components/dashboard/ActionGrid.vue',
    'frontend/garmetix-web/components/dashboard/HealthGrid.vue',
    'frontend/garmetix-web/components/dashboard/TrendChart.vue',
    'frontend/garmetix-web/components/dashboard/ItemList.vue',
    'frontend/garmetix-web/components/dashboard/DataTable.vue',
    'frontend/garmetix-web/pages/dashboard/store-manager/index.vue',
    'frontend/garmetix-web/pages/dashboard/business/index.vue',
    'frontend/garmetix-web/utils/appVersion.ts',
    'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs',
    'docs/stages/stage-7/TODO.md',
    'docs/stages/stage-7/IMPLEMENTATION-MAP.md',
]

for rel in required:
    if not (root / rel).exists():
        errors.append(f'Missing required file: {rel}')

store = (root / 'frontend/garmetix-web/pages/dashboard/store-manager/index.vue').read_text()
business = (root / 'frontend/garmetix-web/pages/dashboard/business/index.vue').read_text()
components = [
    'DashboardPageHero',
    'DashboardMetricGrid',
    'DashboardActionGrid',
    'DashboardHealthGrid',
    'DashboardTrendChart',
    'DashboardItemList',
]
for name in components:
    if name not in store:
        errors.append(f'Store dashboard does not use {name}')
for name in components + ['DashboardDataTable']:
    if name not in business:
        errors.append(f'Business dashboard does not use {name}')

for rel in [
    'frontend/garmetix-web/components/dashboard/PageHero.vue',
    'frontend/garmetix-web/components/dashboard/MetricGrid.vue',
    'frontend/garmetix-web/components/dashboard/ActionGrid.vue',
    'frontend/garmetix-web/components/dashboard/HealthGrid.vue',
    'frontend/garmetix-web/components/dashboard/TrendChart.vue',
    'frontend/garmetix-web/components/dashboard/ItemList.vue',
    'frontend/garmetix-web/components/dashboard/DataTable.vue',
]:
    text = (root / rel).read_text()
    if '<script setup' not in text or '<template>' not in text:
        errors.append(f'{rel} is not a valid Vue SFC shape')

app_version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
api_version = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
package_json = (root / 'frontend/garmetix-web/package.json').read_text()
for token in ['3.8.0', 'Stage 7I', 'GARMETIX-7I-20260610-380']:
    if token not in app_version:
        errors.append(f'Frontend version missing {token}')
    if token not in api_version:
        errors.append(f'Backend version missing {token}')
if '"version": "3.8.0"' not in package_json:
    errors.append('Frontend package.json version was not updated to 3.8.0')

if 'NUXT_PUBLIC_DASHBOARD_SHELL=legacy' not in (root / 'docs/stages/stage-7/IMPLEMENTATION-MAP.md').read_text():
    errors.append('Rollback instruction missing from implementation map')

if errors:
    print('Stage 7I static validation failed:')
    for error in errors:
        print('-', error)
    sys.exit(1)

print('Stage 7I static validation passed.')
print('- Reusable dashboard components exist.')
print('- Store manager and business dashboards use shared components.')
print('- Version identity updated to 3.8.0 / Stage 7I.')
print('- Revert documentation preserved.')

from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = []

def check(name, condition):
    checks.append((name, bool(condition)))

app_shell = (root / 'frontend/garmetix-web/components/AppShell.vue').read_text()
store_page = (root / 'frontend/garmetix-web/pages/dashboard/store-manager/index.vue').read_text()
business_page = (root / 'frontend/garmetix-web/pages/dashboard/business/index.vue').read_text()
dashboard_index = root / 'frontend/garmetix-web/pages/dashboard/index.vue'
endpoints = (root / 'backend/Garmetix.Api/Dashboard/DashboardEndpoints.cs').read_text()
dtos = (root / 'backend/Garmetix.Api/Dashboard/DashboardDtos.cs').read_text()
app_version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
app_info = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()

check('Version is 3.1.0 in frontend', "APP_VERSION = '3.1.0'" in app_version)
check('Version is 3.1.0 in backend', 'Version = "3.1.0"' in app_info)
check('Build code is Stage 7B', 'GARMETIX-7B-20260610-310' in app_version and 'GARMETIX-7B-20260610-310' in app_info)
check('Smart dashboard menu exists', "to: '/dashboard'" in app_shell and 'Smart Dashboard' in app_shell)
check('Topbar dashboard shortcut exists', 'dashboardHomePath' in app_shell and 'label="Dashboard"' in app_shell)
check('Smart dashboard page exists', dashboard_index.exists() and 'dashboard/home' in dashboard_index.read_text())
check('Dashboard home endpoint exists', 'MapGet("/home", HomeAsync)' in endpoints and 'DashboardHomeDto' in dtos)
check('Quick action DTO exists', 'DashboardQuickActionDto' in dtos and 'quickActions' in store_page and 'quickActions' in business_page)
check('Health signal DTO exists', 'DashboardHealthSignalDto' in dtos and 'healthSignals' in store_page and 'healthSignals' in business_page)
check('Store group performance exists', 'StoreGroupPerformanceDto' in dtos and 'StoreGroupPerformanceAsync' in endpoints and 'storeGroups' in business_page)
check('Legacy shell revert preserved', 'NUXT_PUBLIC_DASHBOARD_SHELL=legacy' in (root / 'docs/stages/stage-7/IMPLEMENTATION-MAP.md').read_text())

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(f"{'PASS' if ok else 'FAIL'}: {name}")

if failed:
    raise SystemExit(f"Failed checks: {', '.join(failed)}")

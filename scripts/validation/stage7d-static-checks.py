from pathlib import Path

root = Path(__file__).resolve().parents[2]
app_shell = (root / 'frontend/garmetix-web/components/AppShell.vue').read_text()
css = (root / 'frontend/garmetix-web/assets/css/main.css').read_text()
app_version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
backend_info = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
package_json = (root / 'frontend/garmetix-web/package.json').read_text()

checks = []

def check(name, condition):
    checks.append((name, bool(condition)))

check('frontend version is 3.3.0', "APP_VERSION = '3.3.0'" in app_version)
check('backend version is 3.3.0', 'Version = "3.3.0"' in backend_info)
check('build code is Stage 7D', 'GARMETIX-7D-20260610-330' in app_version and 'GARMETIX-7D-20260610-330' in backend_info)
check('package version is 3.3.0', '"version": "3.3.0"' in package_json)
check('sidebar collapsed state controlled', 'v-model:collapsed="sidebarCollapsed"' in app_shell)
check('sidebar open state controlled', 'v-model:open="sidebarOpen"' in app_shell)
check('collapsed size is set', ':collapsed-size="4"' in app_shell)
check('dashboard group uses rem sizing', 'unit="rem"' in app_shell)
check('Ctrl+B shortcut exists', "key.toLowerCase() === 'b'" in app_shell)
check('primary navigation computed exists', 'navigationPrimaryItems' in app_shell)
check('utility bottom navigation computed exists', 'navigationUtilityItems' in app_shell)
check('account dropdown exists', 'accountDropdownItems' in app_shell and '<UDropdownMenu' in app_shell)
check('legacy shell revert preserved', 'dashboardShell === \'legacy\'' in app_shell)
check('Stage 7D CSS added', 'Stage 7D dashboard-sidebar behavior polish' in css and 'dashboard-sidebar-utility' in css)
check('footer account trigger style added', 'dashboard-account-trigger' in css)

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(('PASS' if ok else 'FAIL') + ': ' + name)

if failed:
    raise SystemExit('\nStage 7D static validation failed: ' + ', '.join(failed))

print('\nStage 7D static validation passed.')

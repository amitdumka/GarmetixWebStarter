from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
checks = []

def check(name, condition):
    checks.append((name, bool(condition)))

app_shell = (root / 'frontend/garmetix-web/components/AppShell.vue').read_text()
app_version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
backend_info = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
map_page = root / 'frontend/garmetix-web/pages/dashboard/map/index.vue'
css = (root / 'frontend/garmetix-web/assets/css/main.css').read_text()

check('frontend version is 3.2.0', "APP_VERSION = '3.2.0'" in app_version)
check('backend version is 3.2.0', 'Version = "3.2.0"' in backend_info)
check('Stage 7C build code set', 'GARMETIX-7C-20260610-320' in app_version and 'GARMETIX-7C-20260610-320' in backend_info)
check('Dashboard Map page exists', map_page.exists())
check('Dashboard Map linked from shell', "/dashboard/map" in app_shell and 'Dashboard Map' in app_shell)
check('breadcrumbs/context bar implemented', 'breadcrumbItems' in app_shell and 'dashboard-context-bar' in app_shell)
check('favorites implemented', 'favoritePaths' in app_shell and 'garmetix.favoritePaths' in app_shell and 'toggleFavorite' in app_shell)
check('recent pages implemented', 'recentPaths' in app_shell and 'garmetix.recentPaths' in app_shell)
check('keyboard command shortcut implemented', 'handleShellShortcut' in app_shell and 'ctrlKey' in app_shell and 'openCommand' in app_shell)
check('legacy shell revert preserved', 'dashboardShell === \'legacy\'' in app_shell and 'AppShellLegacy' in app_shell)
check('Stage 7C css added', 'dashboard-map-grid' in css and 'dashboard-context-bar' in css)
check('single script setup in AppShell', app_shell.count('<script setup') == 1 and app_shell.count('</script>') == 1)
check('single script setup in Dashboard Map', map_page.read_text().count('<script setup') == 1 and map_page.read_text().count('</script>') == 1)

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(f"{'PASS' if ok else 'FAIL'}: {name}")
if failed:
    print('\nFAILED CHECKS:')
    for name in failed:
        print(f'- {name}')
    sys.exit(1)
print('\nStage 7C static validation passed.')

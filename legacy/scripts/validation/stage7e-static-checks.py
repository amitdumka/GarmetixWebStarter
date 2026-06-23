from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
app_shell = root / 'frontend/garmetix-web/components/AppShell.vue'
version_file = root / 'frontend/garmetix-web/utils/appVersion.ts'
app_info = root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs'
css = root / 'frontend/garmetix-web/assets/css/main.css'

checks = []

def expect(name, condition):
    checks.append((name, bool(condition)))

shell_text = app_shell.read_text()
version_text = version_file.read_text()
app_info_text = app_info.read_text()
css_text = css.read_text()

expect('Stage 7E version is 3.4.0 in frontend', "APP_VERSION = '3.4.0'" in version_text)
expect('Stage 7E build code in frontend', "GARMETIX-7E-20260610-340" in version_text)
expect('Stage 7E version is 3.4.0 in backend', 'Version = "3.4.0"' in app_info_text)
expect('Stage 7E build code in backend', 'GARMETIX-7E-20260610-340' in app_info_text)
expect('Sidebar bulky scope card removed from AppShell template', 'dashboard-scope-card' not in shell_text)
expect('Sidebar bulky clock card removed from AppShell template', 'dashboard-clock' not in shell_text)
expect('Compact sidebar footer actions added', 'dashboard-sidebar-mini-actions' in shell_text)
expect('System status dropdown added', 'systemStatusDropdownItems' in shell_text)
expect('Topbar API status dropdown added', ':items="systemStatusDropdownItems"' in shell_text and ':label="apiBadge.label"' in shell_text)
expect('Context bar workspace/status indicators added', 'dashboard-context-workspace' in shell_text and 'dashboard-context-status' in shell_text)
expect('CSS for Stage 7E added', 'Stage 7E sidebar status cleanup' in css_text)
expect('Legacy shell revert option preserved', 'NUXT_PUBLIC_DASHBOARD_SHELL=legacy' in (root / 'docs/stages/stage-7/TODO.md').read_text() or 'dashboardShell' in shell_text)

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(('PASS' if ok else 'FAIL') + ': ' + name)

if failed:
    print('\nFAILED CHECKS:')
    for name in failed:
        print('- ' + name)
    sys.exit(1)

print('\nStage 7E static checks passed.')

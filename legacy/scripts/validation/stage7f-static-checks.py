from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[2]
app_shell = root / 'frontend/garmetix-web/components/AppShell.vue'
version_file = root / 'frontend/garmetix-web/utils/appVersion.ts'
app_info = root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs'
todo = root / 'docs/stages/stage-7/TODO.md'

checks = []

def expect(name, condition):
    checks.append((name, bool(condition)))

shell_text = app_shell.read_text()
version_text = version_file.read_text()
app_info_text = app_info.read_text()
todo_text = todo.read_text() if todo.exists() else ''

module_groups_match = re.search(r"const moduleGroups: MenuGroup\[\] = \[(.*?)\n\]\n\nconst shellCompanies", shell_text, re.S)
module_groups_text = module_groups_match.group(1) if module_groups_match else shell_text

expect('Stage 7F version is 3.5.0 in frontend', "APP_VERSION = '3.5.0'" in version_text)
expect('Stage 7F build code in frontend', "GARMETIX-7F-20260610-350" in version_text)
expect('Stage 7F version is 3.5.0 in backend', 'Version = "3.5.0"' in app_info_text)
expect('Stage 7F build code in backend', 'GARMETIX-7F-20260610-350' in app_info_text)
expect('Account group removed from main sidebar modules', "label: 'Account'" not in module_groups_text)
expect('Help group removed from main sidebar modules', "label: 'Help'" not in module_groups_text)
expect('Utility sidebar navigation contains only Admin', "const utilityNavigationLabels = ['Admin']" in shell_text)
expect('Workspace standalone footer button removed', 'label="Workspace" block' not in shell_text)
expect('Status footer now includes workspace access', 'Status & Workspace' in shell_text and 'Change workspace' in shell_text)
expect('Account footer dropdown still has profile link', "{ label: 'My Profile'" in shell_text and "to: '/profile'" in shell_text)
expect('Footer dropdown still has help links', "to: '/about-us'" in shell_text and "to: '/contact-us'" in shell_text and "to: '/faq'" in shell_text)
expect('Legacy shell revert option preserved', 'NUXT_PUBLIC_DASHBOARD_SHELL=legacy' in todo_text or 'dashboardShell' in shell_text)

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(('PASS' if ok else 'FAIL') + ': ' + name)

if failed:
    print('\nFAILED CHECKS:')
    for name in failed:
        print('- ' + name)
    sys.exit(1)

print('\nStage 7F static checks passed.')

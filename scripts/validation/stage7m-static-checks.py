from pathlib import Path
import re, sys
root = Path(__file__).resolve().parents[2]
frontend = root / 'frontend/garmetix-web'
backend = root / 'backend/Garmetix.Api'
checks = []

def check(name, condition):
    checks.append((name, bool(condition)))

app_shell = (frontend / 'components/AppShell.vue').read_text()
access = (frontend / 'composables/useAccessControl.ts').read_text()
app_version = (frontend / 'utils/appVersion.ts').read_text()
app_info = (backend / 'AppInfo/AppInfoEndpoints.cs').read_text()
css = (frontend / 'assets/css/main.css').read_text()
auth = (frontend / 'components/AuthScreen.vue').read_text()
store_dash = (frontend / 'pages/dashboard/store-manager/index.vue').read_text()
company_dash = (frontend / 'pages/dashboard/business/index.vue').read_text()
ui_audit = (frontend / 'pages/ui-audit/index.vue').read_text()

check('frontend version 3.12.0', "APP_VERSION = '3.12.0'" in app_version)
check('backend version 3.12.0', 'Version = "3.12.0"' in app_info)
check('build code 7M frontend', 'GARMETIX-7M-20260611-3120' in app_version)
check('build code 7M backend', 'GARMETIX-7M-20260611-3120' in app_info)
check('dashboard hero compact title size', 'clamp(22px, 2.3vw, 28px)' in css)
check('sidebar subtitle version only', 'v{{ APP_VERSION }}' in app_shell and 'Dashboard shell · v' not in app_shell)
check('status label renamed', 'label="Status"' in app_shell and 'Status & Workspace' not in app_shell)
for group in ['Sales', 'Purchase', 'Inventory', 'Accounting', 'CRM', 'GST', 'Reports', 'Off Book', 'Data', 'Maintenance', 'System']:
    check(f'menu group {group}', f"label: '{group}'" in app_shell)
check('reports removed from dashboard group', "{ to: '/reports', label: 'Reports Center'" in app_shell and "path: '/reports', label: 'Reports Center', module: 'Reports'" in access)
check('gst reports under GST', "path: '/gst-reports', label: 'GST Reports', module: 'GST'" in access)
check('non gst under off book', "path: '/non-gst-goods', label: 'Non-GST Goods', module: 'Off Book'" in access)
check('only active navigation group opens', 'defaultOpen: group.items.some((item) => isActive(item.to))' in app_shell)
check('navigation remounts per route', ':key="`primary-${route.path}`"' in app_shell and ':key="`utility-${route.path}`"' in app_shell)
check('login technical badges removed', 'JWT protected sessions' not in auth and 'Self-service password recovery' not in auth and 'Garmetix Web' not in auth)
check('store dashboard contextual title', 'storeDashboardTitle' in store_dash and 'badge="Store"' in store_dash)
check('company dashboard contextual title', 'companyDashboardTitle' in company_dash and 'badge="Company"' in company_dash)
check('ui audit before v4 todo retained', 'Required UI Layout Audit Before Stage 8 / v4.0' in (root/'docs/stages/stage-7/TODO.md').read_text())
check('visible stage labels removed from frontend pages', not re.search(r'>\s*Stage\s+[0-9][A-Z]?\s*<', '\n'.join(p.read_text(errors='ignore') for p in (frontend/'pages').rglob('*.vue'))))

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(('PASS' if ok else 'FAIL') + ' - ' + name)
if failed:
    print('\nFAILED checks:')
    for name in failed:
        print('- ' + name)
    sys.exit(1)
print('\nStage 7M static validation passed.')

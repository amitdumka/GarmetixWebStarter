from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
checks = []

def check(path, needle, label):
    text = (root / path).read_text(encoding='utf-8')
    ok = needle in text
    checks.append((label, ok))
    if not ok:
        print(f'FAIL: {label} missing {needle!r} in {path}')

check('frontend/garmetix-web/composables/useAccessControl.ts', 'routeRules', 'central route access map')
check('frontend/garmetix-web/composables/useAccessControl.ts', 'rolesForUser', 'role normalization')
check('frontend/garmetix-web/composables/useAccessControl.ts', 'checkPath', 'path decision helper')
check('frontend/garmetix-web/middleware/auth.global.ts', 'access.checkPath', 'global access guard')
check('frontend/garmetix-web/middleware/auth.global.ts', '/access-denied', 'access denied redirect')
check('frontend/garmetix-web/pages/access-denied.vue', 'Access denied', 'access denied page')
check('frontend/garmetix-web/components/AppShell.vue', 'access.canAccessPath', 'app shell menu filtering')
check('frontend/garmetix-web/components/AppShell.vue', 'sanitizeDropdownGroups', 'dropdown access filtering')
check('frontend/garmetix-web/pages/dashboard/map/index.vue', 'Permission-aware access map', 'dashboard map access matrix')
check('frontend/garmetix-web/utils/appVersion.ts', "APP_VERSION = '3.6.0'", 'frontend version')
check('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs', 'Version = "3.6.0"', 'backend version')

if not all(ok for _, ok in checks):
    sys.exit(1)

print('Stage 7G static checks passed:')
for label, _ in checks:
    print(f'- {label}')

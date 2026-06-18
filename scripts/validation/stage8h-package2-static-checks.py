from pathlib import Path

root = Path(__file__).resolve().parents[2]

def read(path):
    return (root / path).read_text(encoding='utf-8')

middleware = read('frontend/garmetix-web/middleware/auth.global.ts')
access = read('frontend/garmetix-web/composables/useAccessControl.ts')
legacy = read('frontend/garmetix-web/components/AppShellLegacy.vue')
shell = read('frontend/garmetix-web/components/AppShell.vue')
payroll = read('frontend/garmetix-web/pages/payroll/index.vue')
hr = read('frontend/garmetix-web/pages/hr/index.vue')
purchase = read('frontend/garmetix-web/pages/purchase/index.vue')
purchase_new = read('frontend/garmetix-web/pages/purchase/new.vue')
vendor_payments = read('frontend/garmetix-web/pages/vendor-payments/index.vue')
matrix = read('backend/Garmetix.Api/Auth/AccessPermissionMatrix.cs')
program = read('backend/Garmetix.Api/Program.cs')
version = read('frontend/garmetix-web/utils/appVersion.ts')
backend_version = read('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs')

checks = {
    'legacy overview menu admin owner': "label: 'Legacy Overview'" in shell and "roles: ['admin', 'owner']" in shell,
    'root direct redirect for non admin': "to.path === '/'" in middleware and "navigateTo('/dashboard')" in middleware and "auth.canSeeAdmin.value" in middleware,
    'root route rule admin owner': "path: '/'" in access and "roles: ['admin', 'owner']" in access,
    'legacy shell filters access': "useAccessControl()" in legacy and "access.canAccessPath(item.to)" in legacy and "item.to !== '/' || auth.canSeeAdmin.value" in legacy,
    'payroll backend policy store manager remote accountant': "Role(LoginRole.StoreManager)" in matrix and "Role(LoginRole.RemoteAccountant)" in matrix and "[GarmetixPolicies.Payroll]" in matrix,
    'salary structures restricted': "['accountant', 'remoteaccountant', 'payroll']" in payroll and "canManageSalaryStructures.value ? api.list<any>('salary-structures')" in payroll,
    'attendance add action present': 'label="Add Attendance"' in hr and 'startAttendanceCreate' in hr,
    'password reset smtp message present': 'Password reset email is not configured on this server' in program,
    'purchase full page route present': "navigateTo('/purchase/new')" in purchase and 'New Purchase Inward' in purchase_new,
    'vendor payment page present': "paymentType = ref<'invoice' | 'advance'>" in vendor_payments and "purchase/payments/advance" in vendor_payments and "payment-voucher" in vendor_payments,
    'version updated': "APP_VERSION = '4.7.2'" in version and 'GARMETIX-8H-20260617-4720' in version and 'Version = "4.7.2"' in backend_version,
}

failed = [name for name, ok in checks.items() if not ok]
for name, ok in checks.items():
    print(f"[{'PASS' if ok else 'FAIL'}] {name}")
if failed:
    raise SystemExit(f"Stage 8H Package 2 checks failed: {failed}")
print('Stage 8H Package 2 static validation passed.')

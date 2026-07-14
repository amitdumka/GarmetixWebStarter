from pathlib import Path
root = Path(__file__).resolve().parents[2]
required = [
    root / 'frontend/garmetix-web/pages/purchase/new.vue',
    root / 'frontend/garmetix-web/pages/vendor-payments/index.vue',
    root / 'docs/stages/stage-8/Stage8G-Package8-AccessHrPayrollPurchase-v4.6.7-Notes.md',
]
missing = [str(p.relative_to(root)) for p in required if not p.exists()]
if missing:
    raise SystemExit(f'Missing Stage 8G Package 8 files: {missing}')
app = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
api = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
for token in ['4.7.2', 'Stage 8H Package 3 Purchase and Voucher Crash Hotfix', 'GARMETIX-8H-20260617-4720']:
    if token not in app or token not in api:
        raise SystemExit(f'Current version token missing: {token}')
shell = (root / 'frontend/garmetix-web/components/AppShell.vue').read_text()
for token in ["roles: ['admin', 'owner']", "to: '/purchase/new'", "to: '/vendor-payments'"]:
    if token not in shell:
        raise SystemExit(f'Menu/access token missing: {token}')
access = (root / 'frontend/garmetix-web/composables/useAccessControl.ts').read_text()
for token in ["path: '/purchase/new'", "path: '/vendor-payments'", "'storeManager', 'payroll'"]:
    if token not in access:
        raise SystemExit(f'Access rule token missing: {token}')
payroll = (root / 'frontend/garmetix-web/pages/payroll/index.vue').read_text()
for token in ['canManageSalaryStructures', 'visibleTabs', 'Salary structures restricted']:
    if token not in payroll:
        raise SystemExit(f'Payroll visibility token missing: {token}')
hr = (root / 'frontend/garmetix-web/pages/hr/index.vue').read_text()
if 'Add Attendance' not in hr or 'startAttendanceCreate' not in hr:
    raise SystemExit('Attendance add action missing')
purchase = (root / 'frontend/garmetix-web/pages/purchase/index.vue').read_text()
if "navigateTo('/purchase/new')" not in purchase:
    raise SystemExit('Purchase register does not navigate to full-page inward')
endpoints = (root / 'backend/Garmetix.Api/Purchase/PurchaseEndpoints.cs').read_text()
for token in ['GetRecentPurchasePaymentsAsync', 'CreateVendorAdvancePaymentAsync', '/payments/recent', '/payments/advance']:
    if token not in endpoints:
        raise SystemExit(f'Purchase payment endpoint token missing: {token}')
program = (root / 'backend/Garmetix.Api/Program.cs').read_text()
if 'Password reset email is not configured on this server' not in program:
    raise SystemExit('Password reset SMTP diagnostic message missing')
print('Stage 8G Package 8 static validation passed.')

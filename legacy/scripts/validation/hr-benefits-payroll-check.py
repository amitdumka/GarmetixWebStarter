from pathlib import Path
import sys
root = Path(__file__).resolve().parents[2]
checks = []
def read(path):
    p = root / path
    return p.read_text(encoding='utf-8') if p.exists() else ''
def exists(path): return (root / path).exists()
def add(name, ok): checks.append((name, bool(ok)))

employee = read('backend/Garmetix.Domain/Generated/Models/HRM/Employee.cs')
db = read('backend/Garmetix.Infrastructure/Data/GarmetixDbContext.cs')
hr = read('backend/Garmetix.Api/Hr/HrEndpoints.cs')
dtos = read('backend/Garmetix.Api/Hr/HrDtos.cs')
payroll = read('backend/Garmetix.Api/Payroll/PayrollService.cs')
page = read('frontend/garmetix-web/pages/hr-benefits/index.vue')
access = read('frontend/garmetix-web/composables/useAccessControl.ts')
menu = read('frontend/garmetix-web/components/AppShell.vue')
catalog = read('backend/Garmetix.Api/Testing/TestAutomationCatalog.cs')
current = read('scripts/validation/current-release-checks.py')

add('employee payroll adjustment model', 'class EmployeePayrollAdjustment' in employee and all(t in employee for t in ['AdjustmentType', 'RecoverFromSalary', 'PfEmployee', 'PfEmployer', 'GratuityAmount']))
add('dbset and indexes', 'EmployeePayrollAdjustments' in db and 'HasIndex(item => new { item.CompanyId, item.StoreId, item.EmployeeId' in db)
add('api hr-payroll adjustments', all(t in hr for t in ['/api/hr-payroll', '/adjustments/summary', 'CreatePayrollAdjustmentAsync', 'DeletePayrollAdjustmentAsync']))
add('dto contract', all(t in dtos for t in ['EmployeePayrollAdjustmentRequest', 'EmployeeBenefitSummaryDto', 'EmployeePayrollAdjustmentRowDto']))
add('payroll integration', all(t in payroll for t in ['CalculateBenefitAdvanceAsync', 'Bonus', 'LeaveEncashment', 'PfEmployee', 'GratuityAmount']))
add('frontend hr benefits page', exists('frontend/garmetix-web/pages/hr-benefits/index.vue') and all(t in page for t in ['Salary Advance', 'Leave Encashment', 'PF', 'Gratuity', 'hr-payroll/adjustments']))
add('route/menu access', "/hr-benefits" in access and "/hr-benefits" in menu)
add('test automation catalog', 'HR_EMPLOYEE_MASTER_ACCEPTANCE' in catalog and 'HR_BENEFITS_PAYROLL_ACCEPTANCE' in catalog)
add('linux drills', exists('scripts/linux/hr-employee-master-acceptance-drill.sh') and exists('scripts/linux/hr-benefits-payroll-acceptance-drill.sh'))
add('current checks include package23b hotfix', 'stage8i-package23b-static-checks.py' in current and 'v4.9.24' in current)
add('package23 docs', exists('docs/stages/stage-8/Stage8I-Package23-HrBenefitsPayroll-v4.9.22-Notes.md') and exists('docs/operations/HR-Benefits-Payroll-v4.9.22.md'))

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(f"{'OK' if ok else 'FAIL'} - {name}")
if failed:
    print('\nHR benefits/payroll validation failed:', ', '.join(failed))
    sys.exit(1)
print('\nHR benefits/payroll validation passed.')

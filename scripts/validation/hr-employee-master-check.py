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
hr_endpoints = read('backend/Garmetix.Api/Hr/HrEndpoints.cs')
hr_page = read('frontend/garmetix-web/pages/hr/index.vue')
access = read('frontend/garmetix-web/composables/useAccessControl.ts')
migration = read('backend/Garmetix.Infrastructure/Data/Migrations/20260619094000_AddHrEmployeeMasterAndBenefits.cs')
app_info = read('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs')
app_version = read('frontend/garmetix-web/utils/appVersion.ts')

add('employee master fields', all(term in employee for term in ['EmployeeCode', 'FatherOrHusbandName', 'Department', 'Designation', 'MonthlySalary', 'DailyWage', 'PhotoDataUrl', 'EmergencyContact']))
add('employee lifecycle status fields', all(term in employee for term in ['EmployeeStatus', 'ExitReason', 'LeavingDate']))
add('employee document fields', all(term in employee for term in ['PAN', 'Aadhar', 'BankAccountNumber', 'IFSC', 'ESINumber', 'PFNumber']))
add('employee summary endpoint', '/employee-master/summary' in hr_endpoints and 'EmployeeMasterSummaryDto' in read('backend/Garmetix.Api/Hr/HrDtos.cs'))
add('employee id card endpoint', '/employees/{id:guid}/id-card' in hr_endpoints and 'EmployeeIdCardDto' in read('backend/Garmetix.Api/Hr/HrDtos.cs'))
add('frontend photo upload', 'onEmployeePhotoSelected' in hr_page and 'photoDataUrl' in hr_page and 'employee-id-card' in hr_page)
add('frontend employee fields', all(term in hr_page for term in ['Employee code', 'Father/Husband name', 'Department', 'Designation', 'Bank account number', 'Emergency contact']))
add('migration employee fields', 'ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "EmployeeCode"' in migration and 'PhotoDataUrl' in migration)
add('route access hr employee master', "'/hr', label: 'HR Employee Master'" in access)
add('package22 docs', exists('docs/stages/stage-8/Stage8I-Package22-HrEmployeeMasterUpgrade-v4.9.21-Notes.md') and exists('docs/operations/HR-Employee-Master-v4.9.21.md'))
add('version includes package23 final', '4.9.24' in app_info and 'GARMETIX-8I-20260619-49240' in app_info and '4.9.24' in app_version)

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(f"{'OK' if ok else 'FAIL'} - {name}")
if failed:
    print('\nHR employee master validation failed:', ', '.join(failed))
    sys.exit(1)
print('\nHR employee master validation passed.')

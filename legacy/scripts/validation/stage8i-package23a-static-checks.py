from pathlib import Path
import sys
root = Path(__file__).resolve().parents[2]
checks = []
def read(path):
    p = root / path
    return p.read_text(encoding='utf-8') if p.exists() else ''
def exists(path): return (root / path).exists()
def add(name, ok): checks.append((name, bool(ok)))

repair = read('backend/Garmetix.Api/Database/DatabaseSchemaRepairService.cs')
app_info = read('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs')
app_version = read('frontend/garmetix-web/utils/appVersion.ts')
current = read('scripts/validation/current-release-checks.py')
readme = read('README.md')
roadmap = read('docs/planning/CURRENT-ROADMAP.md')

add('hr schema repair function exists', 'RepairHrEmployeeMasterAndBenefitsAsync' in repair)
add('repair called by startup drift repair', 'await RepairHrEmployeeMasterAndBenefitsAsync(db, logger, cancellationToken);' in repair)
add('employee columns repaired', all(t in repair for t in ['ADD COLUMN IF NOT EXISTS "BankAccountName"', 'ADD COLUMN IF NOT EXISTS "EmployeeCode"', 'ADD COLUMN IF NOT EXISTS "PhotoDataUrl"', 'ADD COLUMN IF NOT EXISTS "PFNumber"']))
add('employee payroll adjustment table repaired', 'CREATE TABLE IF NOT EXISTS "EmployeePayrollAdjustments"' in repair and 'ADD COLUMN IF NOT EXISTS "GratuityAmount"' in repair)
add('version metadata package23a', 'Version = "4.9.23"' in app_info and 'Stage 8I Package 23A HR Schema Repair Hotfix' in app_info and 'GARMETIX-8I-20260619-49230' in app_info)
add('frontend version package23a', "APP_VERSION = '4.9.23'" in app_version and 'HR schema repair now auto-adds missing Package 22/23 employee columns' in app_version)
add('current release includes package23a', 'stage8i-package23a-static-checks.py' in current and 'v4.9.23' in current)
add('docs updated', 'Stage 8I Package 23A HR Schema Repair Hotfix / v4.9.23' in roadmap and exists('docs/stages/stage-8/Stage8I-Package23A-HrSchemaRepairHotfix-v4.9.23-Notes.md'))
add('readme updated', 'Stage 8I Package 23A HR Schema Repair Hotfix v4.9.23' in readme and 'GARMETIX-8I-20260619-49230' in readme)

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(f"{'OK' if ok else 'FAIL'} - {name}")
if failed:
    print('\nStage 8I Package 23A static checks failed:', ', '.join(failed), file=sys.stderr)
    sys.exit(1)
print('\nStage 8I Package 23A static checks passed.')

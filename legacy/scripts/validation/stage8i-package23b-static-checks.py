from pathlib import Path
import json
import sys
root = Path(__file__).resolve().parents[2]
checks = []
def read(path):
    p = root / path
    return p.read_text(encoding='utf-8') if p.exists() else ''
def exists(path): return (root / path).exists()
def add(name, ok): checks.append((name, bool(ok)))

program = read('backend/Garmetix.Api/Program.cs')
app_info = read('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs')
app_version = read('frontend/garmetix-web/utils/appVersion.ts')
package_json = json.loads(read('frontend/garmetix-web/package.json'))
package_lock = json.loads(read('frontend/garmetix-web/package-lock.json'))
current = read('scripts/validation/current-release-checks.py')
readme = read('README.md')
roadmap = read('docs/planning/CURRENT-ROADMAP.md')
csproj = read('backend/Garmetix.Api/Garmetix.Api.csproj')

employee_prepare_start = program.find('static async Task<string?> PrepareEmployeeMasterAsync')
employee_prepare = program[employee_prepare_start:program.find('static async Task<string?> EnsureUniqueDailyRecordAsync', employee_prepare_start)] if employee_prepare_start >= 0 else ''

add('employee save sequence avoids DefaultIfEmpty', 'DefaultIfEmpty(0)' not in employee_prepare and 'Select(item => (int?)item.EmpId)' in employee_prepare and 'MaxAsync(cancellationToken) ?? 0' in employee_prepare)
add('employee sequence only generated when missing', 'if (employee.EmpId <= 0)' in employee_prepare and 'employee.EmpId = maxExistingEmpId + 1;' in employee_prepare)
add('deprecated lucide dependency removed', 'lucide-vue-next' not in package_json.get('dependencies', {}) and 'node_modules/lucide-vue-next' not in package_lock.get('packages', {}))
add('version metadata package23b', 'Version = "4.9.24"' in app_info and 'Stage 8I Package 23B Employee Save Hotfix' in app_info and 'GARMETIX-8I-20260619-49240' in app_info)
add('backend project version package23b', '<Version>4.9.24</Version>' in csproj and '<AssemblyVersion>4.9.24.0</AssemblyVersion>' in csproj and '4.9.24-employee-save-hotfix' in csproj)
add('frontend version package23b', "APP_VERSION = '4.9.24'" in app_version and 'DefaultIfEmpty translation failures' in app_version)
add('current release includes package23b', 'stage8i-package23b-static-checks.py' in current and 'v4.9.24' in current)
add('docs updated', 'Stage 8I Package 23B Employee Save Hotfix / v4.9.24' in roadmap and exists('docs/stages/stage-8/Stage8I-Package23B-EmployeeSaveHotfix-v4.9.24-Notes.md'))
add('readme updated', 'Stage 8I Package 23B Employee Save Hotfix v4.9.24' in readme and 'GARMETIX-8I-20260619-49240' in readme)

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(f"{'OK' if ok else 'FAIL'} - {name}")
if failed:
    print('\nStage 8I Package 23B static checks failed:', ', '.join(failed), file=sys.stderr)
    sys.exit(1)
print('\nStage 8I Package 23B static checks passed.')

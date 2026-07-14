from pathlib import Path
import json, sys
root = Path(__file__).resolve().parents[2]
checks=[]
def read(path):
    p=root/path
    return p.read_text(encoding='utf-8') if p.exists() else ''
def exists(path): return (root/path).exists()
def add(name, ok): checks.append((name,bool(ok)))

pkg=json.loads(read('frontend/garmetix-web/package.json'))
lock=json.loads(read('frontend/garmetix-web/package-lock.json'))
readme=read('README.md')
docs=read('docs/README.md')
backend=read('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs')
appv=read('frontend/garmetix-web/utils/appVersion.ts')
csproj=read('backend/Garmetix.Api/Garmetix.Api.csproj')

add('frontend package version 4.9.22', pkg.get('version') == '4.9.22')
add('frontend lock version 4.9.22', lock.get('version') == '4.9.22' and lock.get('packages', {}).get('', {}).get('version') == '4.9.22')
add('backend version 4.9.22', 'Version = "4.9.22"' in backend and 'GARMETIX-8I-20260619-49220' in backend)
add('frontend app version 4.9.22', "APP_VERSION = '4.9.22'" in appv and 'GARMETIX-8I-20260619-49220' in appv)
add('backend csproj version', '<Version>4.9.22</Version>' in csproj and '<AssemblyVersion>4.9.22.0</AssemblyVersion>' in csproj)
add('readme current package', 'Stage 8I Package 23 HR Benefits and Payroll Adjustments v4.9.22' in readme and 'GARMETIX-8I-20260619-49220' in readme)
add('docs current package', 'Stage 8I Package 23 HR Benefits and Payroll Adjustments v4.9.22' in docs and 'GARMETIX-8I-20260619-49220' in docs)
add('package22/23 docs exist', exists('docs/stages/stage-8/Stage8I-Package22-HrEmployeeMasterUpgrade-v4.9.21-Notes.md') and exists('docs/stages/stage-8/Stage8I-Package23-HrBenefitsPayroll-v4.9.22-Notes.md'))
add('validation scripts exist', exists('scripts/validation/hr-employee-master-check.py') and exists('scripts/validation/hr-benefits-payroll-check.py'))

failed=[n for n,ok in checks if not ok]
for n,ok in checks: print(f"{'OK' if ok else 'FAIL'} - {n}")
if failed:
    print('\nPackage 23 static checks failed:', ', '.join(failed)); sys.exit(1)
print('\nStage 8I Package 23 static checks passed.')

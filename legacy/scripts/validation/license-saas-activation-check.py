#!/usr/bin/env python3
from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
errors: list[str] = []

def read(rel: str) -> str:
    return (ROOT / rel).read_text(encoding='utf-8')

def exists(rel: str) -> bool:
    return (ROOT / rel).exists()

def add(name: str, ok: bool):
    if not ok:
        errors.append(name)

program = read('backend/Garmetix.Api/Program.cs')
options = read('backend/Garmetix.Api/Licensing/LicenseOptions.cs') if exists('backend/Garmetix.Api/Licensing/LicenseOptions.cs') else ''
service = read('backend/Garmetix.Api/Licensing/LicenseActivationService.cs') if exists('backend/Garmetix.Api/Licensing/LicenseActivationService.cs') else ''
endpoints = read('backend/Garmetix.Api/Licensing/LicenseEndpoints.cs') if exists('backend/Garmetix.Api/Licensing/LicenseEndpoints.cs') else ''
middleware = read('backend/Garmetix.Api/Licensing/LicenseEnforcementMiddleware.cs') if exists('backend/Garmetix.Api/Licensing/LicenseEnforcementMiddleware.cs') else ''
page = read('frontend/garmetix-web/pages/license-activation/index.vue') if exists('frontend/garmetix-web/pages/license-activation/index.vue') else ''
access = read('frontend/garmetix-web/composables/useAccessControl.ts')
shell = read('frontend/garmetix-web/components/AppShell.vue')
catalog = read('backend/Garmetix.Api/Testing/TestAutomationCatalog.cs')
runtime = read('backend/Garmetix.Api/Testing/TestAutomationEndpoints.cs')
appsettings = read('backend/Garmetix.Api/appsettings.json')
docker = read('docker-compose.prod.yml')
create_env = read('deploy/create-production-env.sh')
linux_smoke = read('scripts/linux/smoke-test.sh')
windows_smoke = read('scripts/windows/smoke-test.ps1')
app_version = read('frontend/garmetix-web/utils/appVersion.ts')
backend_version = read('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs')
backend_project = read('backend/Garmetix.Api/Garmetix.Api.csproj')

add('license source files exist', all(exists(path) for path in [
    'backend/Garmetix.Api/Licensing/LicenseOptions.cs',
    'backend/Garmetix.Api/Licensing/LicenseDtos.cs',
    'backend/Garmetix.Api/Licensing/LicenseActivationService.cs',
    'backend/Garmetix.Api/Licensing/LicenseEndpoints.cs',
    'backend/Garmetix.Api/Licensing/LicenseEnforcementMiddleware.cs']))
add('license service registered', 'Configure<LicenseOptions>' in program and 'AddSingleton<LicenseActivationService>' in program and 'MapLicenseEndpoints' in program and 'LicenseEnforcementMiddleware' in program)
add('license options contract', all(token in options for token in ['EnforcementEnabled', 'RequireLicenseForOperationalApis', 'ProductCode', 'MasterSecret', 'ActivationFilePath', 'RequiredModulesCsv']))
add('license signed token contract', all(token in service for token in ['GARMETIX-LIC-v1', 'HMACSHA256', 'CryptographicOperations.FixedTimeEquals', 'CreateLicenseKey', 'Fingerprint']))
add('license activation file contract', 'LicenseActivationFileDto' in service and 'File.WriteAllText' in service and 'ActivatedAtUtc' in service)
add('license endpoints contract', '/api/license' in endpoints and '/status' in endpoints and '/generate' in endpoints and '/activate' in endpoints and '/activation' in endpoints and 'RequireAuthorization(GarmetixPolicies.Admin)' in endpoints)
add('license middleware safe allowlist', 'StatusCodes.Status402PaymentRequired' in middleware and '/api/license' in middleware and '/api/auth/login' in middleware and 'RequireLicenseForOperationalApis' in middleware)
add('license appsettings/env wiring', all(token in appsettings for token in ['"License"', '"EnforcementEnabled"', '"ActivationFilePath"', '"RequiredModulesCsv"']) and all(token in docker for token in ['License__EnforcementEnabled', 'License__MasterSecret', './license:/app/license']) and all(token in create_env for token in ['LICENSE_ENFORCEMENT_ENABLED', 'LICENSE_MASTER_SECRET', 'LICENSE_REQUIRED_MODULES']))
add('license frontend page', bool(page) and 'License Activation' in page and 'license/status' in page and 'license/generate' in page and 'license/activate' in page and 'LICENSE_MASTER_SECRET' in page)
add('license route protected', "path: '/license-activation', label: 'License Activation'" in access and "roles: ['admin', 'owner']" in access)
add('license shell menu', "to: '/license-activation'" in shell and "label: 'License Activation'" in shell)
add('license acceptance script', exists('scripts/linux/license-acceptance-drill.sh') and 'license/status' in read('scripts/linux/license-acceptance-drill.sh') and 'GARMETIX_LICENSE_GENERATE_TEST' in read('scripts/linux/license-acceptance-drill.sh'))
add('manifest license acceptance', 'LICENSE_SAAS_ACTIVATION' in catalog and 'license-acceptance-drill.sh' in catalog)
add('runtime license acceptance required', '"LICENSE_SAAS_ACTIVATION"' in runtime)
add('smoke expected version and license manifest', 'GARMETIX_EXPECTED_VERSION:-4.9.24' in linux_smoke and 'LICENSE_SAAS_ACTIVATION' in linux_smoke and 'ExpectedVersion = "4.9.24"' in windows_smoke and 'LICENSE_SAAS_ACTIVATION' in windows_smoke)
add('frontend version current release', "APP_VERSION = '4.9.24'" in app_version and 'GARMETIX-8I-20260619-49240' in app_version)
add('backend version current release', 'Version = "4.9.24"' in backend_version and 'Stage 8I Package 23B Employee Save Hotfix' in backend_version and 'GARMETIX-8I-20260619-49240' in backend_version)
add('backend project version current release', '<Version>4.9.24</Version>' in backend_project and '<AssemblyVersion>4.9.24.0</AssemblyVersion>' in backend_project and '4.9.24-employee-save-hotfix' in backend_project)
add('docs package21', exists('docs/stages/stage-8/Stage8I-Package21-ClientLicenseSaasActivation-v4.9.20-Notes.md') and exists('docs/operations/License-SaaS-Activation-v4.9.20.md'))
add('roadmap package21', 'Stage 8I Package 21 Client License and SaaS Activation / v4.9.20' in read('docs/planning/CURRENT-ROADMAP.md'))
add('issues package21', 'Client licensing/SaaS activation was pending' in read('docs/planning/ISSUES.md'))

if errors:
    print('License SaaS activation check failed:', file=sys.stderr)
    for error in errors:
        print(f' - {error}', file=sys.stderr)
    sys.exit(1)

print('License SaaS activation check passed.')

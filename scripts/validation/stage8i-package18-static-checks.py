#!/usr/bin/env python3
from __future__ import annotations

import json
import re
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

# Lightweight route-access audit inline to avoid nested subprocess hangs on some hosts.
pages_dir = ROOT / 'frontend/garmetix-web/pages'
access = read('frontend/garmetix-web/composables/useAccessControl.ts')
rules: list[tuple[str, bool]] = []
for match in re.finditer(r"\{\s*path:\s*'([^']+)'(?P<body>.*?)\}", access, flags=re.DOTALL):
    path = match.group(1).rstrip('/') or '/'
    exact = bool(re.search(r"exact:\s*true", match.group('body')))
    rules.append((path, exact))

def page_path(vue_file: Path) -> str:
    rel = vue_file.relative_to(pages_dir).with_suffix('')
    parts = list(rel.parts)
    if parts and parts[-1] == 'index':
        parts = parts[:-1]
    return ('/' + '/'.join(parts)).replace('//', '/') or '/'

def covered(path: str) -> bool:
    cleaned = path.rstrip('/') or '/'
    for rule_path, exact in rules:
        if exact and cleaned == rule_path:
            return True
        if not exact and (cleaned == rule_path or cleaned.startswith(f'{rule_path}/')):
            return True
    return False

approved = {'/access-denied', '/[module]'}
missing_routes = [path for path in sorted(page_path(f) for f in pages_dir.rglob('*.vue')) if path not in approved and not covered(path)]
add('frontend route access audit', not missing_routes and 'No explicit page rule is configured' in access)

pkg = json.loads(read('frontend/garmetix-web/package.json'))
lock = json.loads(read('frontend/garmetix-web/package-lock.json'))
app_version = read('frontend/garmetix-web/utils/appVersion.ts')
backend_version = read('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs')
backend_project = read('backend/Garmetix.Api/Garmetix.Api.csproj')
backup_service = read('backend/Garmetix.Api/Backup/DatabaseBackupService.cs')
backup_options = read('backend/Garmetix.Api/Backup/BackupOptions.cs')
backup_page = read('frontend/garmetix-web/pages/backup-maintenance/index.vue')
prod_readiness = read('backend/Garmetix.Api/Production/ProductionReadinessEndpoints.cs')
compose = read('docker-compose.prod.yml')
env_example = read('.env.production.example')
permission_endpoint = read('backend/Garmetix.Api/Production/PermissionAcceptanceEndpoints.cs')
permission_page = read('frontend/garmetix-web/pages/permission-final-acceptance/index.vue')
catalog = read('backend/Garmetix.Api/Testing/TestAutomationCatalog.cs')
runtime = read('backend/Garmetix.Api/Testing/TestAutomationEndpoints.cs')
linux_smoke = read('scripts/linux/smoke-test.sh')
windows_smoke = read('scripts/windows/smoke-test.ps1')
readme = read('README.md')
docs_readme = read('docs/README.md')
roadmap = read('docs/planning/CURRENT-ROADMAP.md')
issues = read('docs/planning/ISSUES.md')

add('frontend package version 4.9.17', pkg.get('version') == '4.9.17')
add('frontend lock version 4.9.17', lock.get('version') == '4.9.17' and lock.get('packages', {}).get('', {}).get('version') == '4.9.17')
add('frontend app version package18', "APP_VERSION = '4.9.17'" in app_version and 'GARMETIX-8I-20260619-49170' in app_version)
add('backend app version package18', 'Version = "4.9.17"' in backend_version and 'Stage 8I Package 18 Role-wise Permission Acceptance' in backend_version and 'GARMETIX-8I-20260619-49170' in backend_version)
add('backend project version package18', '<Version>4.9.17</Version>' in backend_project and '<AssemblyVersion>4.9.17.0</AssemblyVersion>' in backend_project and '4.9.17-role-wise-permission-acceptance' in backend_project)

add('backup retention options', 'RetentionDays' in backup_options and 'KeepMinimum' in backup_options and 'RestoreDrillMarkerPath' in backup_options)
add('backup env and compose wiring', 'BACKUP_RETENTION_DAYS=30' in env_example and 'BACKUP_KEEP_MINIMUM=10' in env_example and 'Backup__RetentionDays' in compose and 'Backup__KeepMinimum' in compose)
add('restore preview checks required tables', 'RequiredRestoreTables' in backup_service and 'RequiredTablesMissing' in backup_service and 'RequiredTablesPresent' in backup_service)
add('backup manifest uses app info', 'AppInfoEndpoints.Stage' in backup_service and 'AppInfoEndpoints.BuildCode' in backup_service)
add('restore drill status in maintenance', 'LastRestoreDrillAtUtc' in backup_service and 'RestoreDrillStatus' in backup_service)
add('production readiness restore drill check', 'BACKUP_RESTORE_DRILL' in prod_readiness and 'backupService.GetMaintenanceStatus()' in prod_readiness)
add('backup page restore dry-run preview', 'Restore Preview / Dry Run' in backup_page and 'previewLocalBackup' in backup_page and 'requiredTablesMissing' in backup_page)
add('backup drill scripts', exists('scripts/linux/backup-restore-drill.sh') and 'garmetix_restore_drill_' in read('scripts/linux/backup-restore-drill.sh'))

for token in ['PermissionModuleCoverageDto', 'PermissionRouteExpectationDto', 'HasRoleMatrixCoverage', 'BuildRouteExpectations', '/backup-maintenance', '/access']:
    add(f'permission endpoint contains {token}', token in permission_endpoint)
for role in ['Admin', 'StoreManager', 'Salesman', 'Accountant', 'HR', 'Payroll', 'PowerUser']:
    add(f'route expectation contains {role}', role in permission_endpoint)
add('permission page matrix and routes', 'Effective permission matrix' in permission_page and 'status?.matrix' in permission_page and 'Route acceptance checklist' in permission_page and 'status?.routeExpectations' in permission_page)
add('permission page readiness uses role matrix', 'hasRoleMatrixCoverage' in permission_page)

add('manifest includes package17/18 checks', 'BACKUP_RESTORE_SAFETY' in catalog and 'PERMISSION_ROLE_ACCEPTANCE' in catalog)
add('runtime includes package17/18 checks', 'BACKUP_RESTORE_SAFETY' in runtime and 'PERMISSION_ROLE_ACCEPTANCE' in runtime)
add('linux smoke package18 expected version', 'GARMETIX_EXPECTED_VERSION:-4.9.17' in linux_smoke and 'BACKUP_RESTORE_SAFETY' in linux_smoke and 'PERMISSION_ROLE_ACCEPTANCE' in linux_smoke)
add('windows smoke package18 expected version', 'ExpectedVersion = "4.9.17"' in windows_smoke and 'BACKUP_RESTORE_SAFETY' in windows_smoke and 'PERMISSION_ROLE_ACCEPTANCE' in windows_smoke)
add('operation docs package17 and package18', exists('docs/operations/Backup-Restore-Data-Safety-v4.9.16.md') and exists('docs/operations/Role-Wise-Permission-Acceptance-v4.9.17.md'))
add('stage notes package17 and package18', exists('docs/stages/stage-8/Stage8I-Package17-BackupRestoreDataSafety-v4.9.16-Notes.md') and exists('docs/stages/stage-8/Stage8I-Package18-RoleWisePermissionAcceptance-v4.9.17-Notes.md'))
add('readme current package updated', 'Stage 8I Package 18 Role-wise Permission Acceptance v4.9.17' in readme and 'GARMETIX-8I-20260619-49170' in readme)
add('docs readme current package updated', 'Stage 8I Package 18 Role-wise Permission Acceptance v4.9.17' in docs_readme and 'GARMETIX-8I-20260619-49170' in docs_readme)
add('roadmap updated package17/18', 'Stage 8I Package 17 Backup, Restore & Data Safety / v4.9.16' in roadmap and 'Stage 8I Package 18 Role-wise Permission Acceptance / v4.9.17' in roadmap)
add('issues updated package17/18', 'Backup restore safety drill added in v4.9.16' in issues and 'Role-wise permission acceptance matrix added in v4.9.17' in issues)

if errors:
    print('Stage 8I Package 18 static checks failed:', file=sys.stderr)
    for error in errors:
        print(f' - {error}', file=sys.stderr)
    if missing_routes:
        print('Missing route rules:', ', '.join(missing_routes), file=sys.stderr)
    sys.exit(1)

print('Stage 8I Package 18 static checks passed.')

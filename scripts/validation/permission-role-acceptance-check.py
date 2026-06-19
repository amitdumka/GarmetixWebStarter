#!/usr/bin/env python3
from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
errors: list[str] = []

def read(rel: str) -> str:
    return (ROOT / rel).read_text(encoding='utf-8')

def add(name: str, ok: bool):
    if not ok:
        errors.append(name)

endpoint = read('backend/Garmetix.Api/Production/PermissionAcceptanceEndpoints.cs')
page = read('frontend/garmetix-web/pages/permission-final-acceptance/index.vue')
catalog = read('backend/Garmetix.Api/Testing/TestAutomationCatalog.cs')
runtime = read('backend/Garmetix.Api/Testing/TestAutomationEndpoints.cs')
access = read('frontend/garmetix-web/composables/useAccessControl.ts')

for token in ['PermissionModuleCoverageDto', 'PermissionRouteExpectationDto', 'HasRoleMatrixCoverage', 'BuildRouteExpectations', '/backup-maintenance', '/access']:
    add(f'permission endpoint contains {token}', token in endpoint)
for role in ['Admin', 'StoreManager', 'Salesman', 'Accountant', 'HR', 'Payroll', 'PowerUser']:
    add(f'route expectation contains {role}', role in endpoint)
add('permission page shows effective matrix', 'Effective permission matrix' in page and 'status?.matrix' in page)
add('permission page shows route acceptance checklist', 'Route acceptance checklist' in page and 'status?.routeExpectations' in page)
add('permission page readiness uses role matrix coverage', 'hasRoleMatrixCoverage' in page)
add('test manifest includes permission role acceptance', 'PERMISSION_ROLE_ACCEPTANCE' in catalog and 'permission-role-acceptance-check.py' in catalog)
add('runtime required codes includes permission role acceptance', 'PERMISSION_ROLE_ACCEPTANCE' in runtime)
add('access control protects permission final acceptance', '/permission-final-acceptance' in access)

if errors:
    print('Permission role acceptance validation failed:', file=sys.stderr)
    for error in errors:
        print(f' - {error}', file=sys.stderr)
    sys.exit(1)

print('Permission role acceptance validation passed.')

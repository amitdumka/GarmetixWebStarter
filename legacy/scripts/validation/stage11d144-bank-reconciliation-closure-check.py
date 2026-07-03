#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
checks = []

def read(path):
    p = ROOT / path
    return p.exists(), p.read_text(encoding='utf-8', errors='ignore') if p.exists() else ''

def add(name, ok):
    checks.append((name, bool(ok)))

files = {
    'endpoint': 'backend/Garmetix.Api/BankReconciliation/BankReconciliationClosureEndpoints.cs',
    'program': 'backend/Garmetix.Api/Program.cs',
    'page': 'frontend/garmetix-web/pages/bank-reconciliation-closure/index.vue',
    'shell': 'frontend/garmetix-web/components/AppShell.vue',
    'legacy_shell': 'frontend/garmetix-web/components/AppShellLegacy.vue',
    'access': 'frontend/garmetix-web/composables/useAccessControl.ts',
    'app_info': 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs',
    'app_version': 'frontend/garmetix-web/utils/appVersion.ts',
    'csproj': 'backend/Garmetix.Api/Garmetix.Api.csproj',
    'release': 'RELEASE-v4.12.59.txt',
    'stage_doc': 'docs/stages/stage-11/Stage11D144-Bank-Reconciliation-Closure-v4.12.59.md',
    'module_doc': 'docs/modules/accounting/bank-reconciliation-closure-v4.12.59.md',
}
texts = {}
for name, path in files.items():
    exists, text = read(path)
    texts[name] = text
    add(f'{name} exists', exists)

add('endpoint route added', 'bank-reconciliation/settlement-closure' in texts['endpoint'] and 'evidence.csv' in texts['endpoint'])
add('program mapped', 'using Garmetix.Api.BankReconciliation;' in texts['program'] and 'MapBankReconciliationClosureEndpoints' in texts['program'])
add('frontend route added', 'Bank Reco Closure' in texts['page'] and 'bank-reconciliation/settlement-closure' in texts['page'])
add('menu links added', '/bank-reconciliation-closure' in texts['shell'] and '/bank-reconciliation-closure' in texts['legacy_shell'])
add('access rule added', '/bank-reconciliation-closure' in texts['access'])
add('version updated', all(token in texts['app_info'] for token in ['4.12.59', 'Stage 11D-144 Bank Reconciliation Closure', 'GARMETIX-11D144-20260703-4259']) and "APP_VERSION = '4.12.59'" in texts['app_version'] and '<Version>4.12.59</Version>' in texts['csproj'])
add('docs mention closure', 'Payment Settlement Closure' in texts['stage_doc'] and 'Critical blockers' in texts['module_doc'])

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(f"{'PASS' if ok else 'FAIL'} {name}")
if failed:
    print('\nFailed checks:', ', '.join(failed))
    sys.exit(1)
print('\nStage 11D-144 static validation passed.')

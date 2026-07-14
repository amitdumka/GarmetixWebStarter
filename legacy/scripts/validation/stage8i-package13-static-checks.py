#!/usr/bin/env python3
from pathlib import Path
import json
import sys

ROOT = Path(__file__).resolve().parents[2]
checks: list[tuple[str, bool]] = []

def text(rel: str) -> str:
    return (ROOT / rel).read_text(encoding='utf-8')

def add(name: str, ok: bool) -> None:
    checks.append((name, ok))

access = text('frontend/garmetix-web/composables/useAccessControl.ts')
for route in [
    '/cash-details',
    '/store-day',
    '/tailoring',
    '/backup-maintenance',
    '/gst-final-acceptance',
    '/print-final-acceptance',
    '/permission-final-acceptance',
    '/post-go-live-acceptance',
    '/stage8g-completion',
]:
    add(f'explicit access rule for {route}', f"path: '{route}'" in access)

cash = text('backend/Garmetix.Api/StoreDay/CashDetailsEndpoints.cs')
add('CashDetails imports GarmetixPolicies', 'using Garmetix.Api.Auth;' in cash)
add('CashDetails group requires Accounting policy', '.RequireAuthorization(GarmetixPolicies.Accounting)' in cash)
add('CashDetails validates payload', 'ValidateCashDetailRequest' in cash and 'Cash note and coin counts cannot be negative' in cash)
add('CashDetails linked store/date hardening', 'Linked Day Opening/Closing cash details cannot move store or date' in cash)
add('CashDetails linked source hardening', 'Linked Day Opening/Closing cash details cannot change source' in cash)
add('CashDetails linked check before mutation', 'var linked = await IsLinkedAsync(db, entity.Id, cancellationToken);' in cash)

store_day = text('backend/Garmetix.Api/StoreDay/StoreDayEndpoints.cs')
add('StoreDay group requires Billing policy', '.RequireAuthorization(GarmetixPolicies.Billing)' in store_day)

pkg = json.loads(text('frontend/garmetix-web/package.json'))
lock = json.loads(text('frontend/garmetix-web/package-lock.json'))
add('package.json version 4.9.12', pkg.get('version') == '4.9.12')
add('package-lock root version 4.9.12', lock.get('version') == '4.9.12' and lock.get('packages', {}).get('', {}).get('version') == '4.9.12')

app_version = text('frontend/garmetix-web/utils/appVersion.ts')
backend_version = text('backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs')
backend_project = text('backend/Garmetix.Api/Garmetix.Api.csproj')
add('app version 4.9.12', "APP_VERSION = '4.9.12'" in app_version)
add('app build code 49120', 'GARMETIX-8I-20260619-49120' in app_version)
add('backend app-info version 4.9.12', 'Version = "4.9.12"' in backend_version and 'GARMETIX-8I-20260619-49120' in backend_version)
add('backend project version 4.9.12', '<Version>4.9.12</Version>' in backend_project and '<AssemblyVersion>4.9.12.0</AssemblyVersion>' in backend_project)

add('operation docs added', (ROOT / 'docs/operations/Production-Stabilization-Repair-Pack-v4.9.12.md').exists())
add('stage notes added', (ROOT / 'docs/stages/stage-8/Stage8I-Package13-ProductionStabilization-v4.9.12-Notes.md').exists())
add('roadmap updated', 'Stage 8I Package 13 Production Stabilization Repair Pack / v4.9.12' in text('docs/planning/CURRENT-ROADMAP.md'))
add('issues reconciled', 'Fixed in v4.9.12' in text('docs/planning/ISSUES.md'))

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(f"{'PASS' if ok else 'FAIL'} - {name}")

if failed:
    print('\nFAILED CHECKS:')
    for name in failed:
        print(f'- {name}')
    sys.exit(1)

print('\nStage 8I Package 13 static checks passed.')

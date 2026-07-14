#!/usr/bin/env python3
from pathlib import Path
import sys

required = {
    'backend/Garmetix.Api/Production/ProductionGoLiveMasterAcceptanceEndpoints.cs': [
        'MapProductionGoLiveMasterAcceptanceEndpoints',
        '/api/production-go-live/master-acceptance',
        'MapFinalOwnerSignoffEndpoints',
        '/api/final-owner-signoff',
        'BuildPrintHtml'
    ],
    'backend/Garmetix.Api/Program.cs': [
        'MapProductionGoLiveMasterAcceptanceEndpoints',
        'MapFinalOwnerSignoffEndpoints'
    ],
    'frontend/garmetix-web/pages/production-go-live-master-acceptance/index.vue': [
        'Production Go-Live Master Acceptance',
        'production-go-live/master-acceptance',
        'CSV evidence',
        'Owner Sign-off'
    ],
    'frontend/garmetix-web/pages/final-owner-signoff/index.vue': [
        'Final Owner Sign-off',
        'final-owner-signoff',
        'Print / PDF',
        'ownerName'
    ],
    'frontend/garmetix-web/components/AppShell.vue': [
        '/production-go-live-master-acceptance',
        '/final-owner-signoff'
    ],
    'frontend/garmetix-web/components/AppShellLegacy.vue': [
        '/production-go-live-master-acceptance',
        '/final-owner-signoff'
    ],
    'frontend/garmetix-web/composables/useAccessControl.ts': [
        '/production-go-live-master-acceptance',
        '/final-owner-signoff'
    ],
    'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs': [
        '4.12.64',
        'Stage 11D-149 Final Owner Sign-off',
        'GARMETIX-11D149-20260703-4264',
        'Stage 11D-148 adds Production Go-Live Master Acceptance'
    ],
    'frontend/garmetix-web/utils/appVersion.ts': [
        '4.12.64',
        'Stage 11D-149 Final Owner Sign-off',
        'GARMETIX-11D149-20260703-4264',
        'Stage 11D-148 adds Production Go-Live Master Acceptance'
    ],
    'docs/stages/stage-11/Stage11D148-Production-Go-Live-Master-Acceptance-v4.12.63.md': [
        'Production Go-Live Master Acceptance'
    ],
    'docs/stages/stage-11/Stage11D149-Final-Owner-Signoff-v4.12.64.md': [
        'Final Owner Sign-off'
    ],
}

missing = []
for filename, needles in required.items():
    path = Path(filename)
    if not path.exists():
        missing.append(f'missing file: {filename}')
        continue
    text = path.read_text(encoding='utf-8')
    for needle in needles:
        if needle not in text:
            missing.append(f'{filename}: missing {needle!r}')

if missing:
    print('Stage 11D-148/149 validation failed:')
    for item in missing:
        print(' -', item)
    sys.exit(1)

print('Stage 11D-148/149 go-live + owner sign-off static validation passed.')

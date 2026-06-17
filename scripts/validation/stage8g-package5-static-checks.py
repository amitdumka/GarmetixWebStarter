from pathlib import Path
root = Path(__file__).resolve().parents[2]
required = [
    root / 'backend/Garmetix.Api/Gstin/GstinEndpoints.cs',
    root / 'backend/Garmetix.Api/Gstin/GstinDtos.cs',
    root / 'scripts/linux/gstin-provider-readiness-check.sh',
    root / 'docs/stages/stage-8/Stage8G-Package5-GstinProviderValidation-v4.6.4-Notes.md',
    root / 'docs/operations/GSTIN-Provider-Validation-v4.6.4.md',
]
missing = [str(path.relative_to(root)) for path in required if not path.exists()]
if missing:
    raise SystemExit(f'Missing Stage 8G Package 5 files: {missing}')
endpoints = (root / 'backend/Garmetix.Api/Gstin/GstinEndpoints.cs').read_text()
for token in ['/provider/status', '/provider/test', 'GarmetixPolicies.Admin']:
    if token not in endpoints:
        raise SystemExit(f'GSTIN endpoint token missing: {token}')
dtos = (root / 'backend/Garmetix.Api/Gstin/GstinDtos.cs').read_text()
for token in ['GstinProviderStatusDto', 'GstinProviderTestRequest', 'GstinProviderTestResponse']:
    if token not in dtos:
        raise SystemExit(f'GSTIN DTO missing: {token}')
frontend = (root / 'frontend/garmetix-web/pages/production-readiness/index.vue').read_text()
for token in ['gstin/provider/status', 'gstin/provider/test', 'GSTIN provider validation']:
    if token not in frontend:
        raise SystemExit(f'Frontend GSTIN readiness token missing: {token}')
version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
for token in ["APP_VERSION = '4.6.8'", 'Stage 8G Package 9 Final Go-Live Acceptance', 'GARMETIX-8G-20260617-4680']:
    if token not in version:
        raise SystemExit(f'Frontend version token missing: {token}')
backend = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
for token in ['Version = "4.6.8"', 'Stage 8G Package 9 Final Go-Live Acceptance', 'GARMETIX-8G-20260617-4680']:
    if token not in backend:
        raise SystemExit(f'Backend version token missing: {token}')
roadmap = (root / 'docs/planning/CURRENT-ROADMAP.md').read_text()
if 'Stage 8G Package 5 GSTIN Provider Validation / v4.6.4' not in roadmap:
    raise SystemExit('Current roadmap missing Stage 8G Package 5 entry')
print('Stage 8G Package 5 static validation passed.')

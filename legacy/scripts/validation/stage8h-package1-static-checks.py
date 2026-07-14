from pathlib import Path
root = Path(__file__).resolve().parents[2]
required = [
    root / 'frontend/garmetix-web/pages/post-go-live-acceptance/index.vue',
    root / 'scripts/linux/post-go-live-acceptance-check.sh',
    root / 'docs/stages/stage-8/Stage8H-Package1-PostGoLiveAcceptance-v4.7.0-Notes.md',
    root / 'docs/operations/Post-Go-Live-Acceptance-v4.7.0.md',
]
missing = [str(p.relative_to(root)) for p in required if not p.exists()]
if missing:
    raise SystemExit(f'Missing Stage 8H Package 1 files: {missing}')

version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
for token in ["APP_VERSION = '4.7.2'", 'Stage 8H Package 3 Purchase and Voucher Crash Hotfix', 'GARMETIX-8H-20260617-4720']:
    if token not in version:
        raise SystemExit(f'Frontend version token missing: {token}')

backend = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
for token in ['Version = "4.7.2"', 'Stage 8H Package 3 Purchase and Voucher Crash Hotfix', 'GARMETIX-8H-20260617-4720']:
    if token not in backend:
        raise SystemExit(f'Backend app info token missing: {token}')

shell = (root / 'frontend/garmetix-web/components/AppShell.vue').read_text()
if '/post-go-live-acceptance' not in shell:
    raise SystemExit('AppShell missing post-go-live acceptance navigation link')

script = (root / 'scripts/linux/post-go-live-acceptance-check.sh').read_text()
for token in ['Legacy Overview', 'Salary Structures', 'Vendor Payments', '/purchase/new']:
    if token not in script:
        raise SystemExit(f'Acceptance script missing checklist item: {token}')

print('Stage 8H Package 1 static validation passed.')

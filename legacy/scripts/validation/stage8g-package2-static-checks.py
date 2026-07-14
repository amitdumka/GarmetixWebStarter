from pathlib import Path
root = Path(__file__).resolve().parents[2]
required = [
    root / 'frontend/garmetix-web/public/garmetix-logo.png',
    root / 'scripts/linux/go-live-readiness-check.sh',
    root / 'scripts/linux/run-backup-restore-drill.sh',
    root / 'docs/stages/stage-8/Stage8G-Package2-GoLiveReadiness-v4.6.1-Notes.md',
]
missing = [str(p.relative_to(root)) for p in required if not p.exists()]
if missing:
    raise SystemExit(f'Missing Stage 8G Package 2 files: {missing}')
version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
if "APP_VERSION = '4." not in version:
    raise SystemExit('Frontend app version not compatible with current Stage 8G release')
backend = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
if 'Version = "4.' not in backend:
    raise SystemExit('Backend app info token missing for current Stage 8G release')
roadmap = (root / 'docs/planning/CURRENT-ROADMAP.md').read_text()
if 'Stage 8G Package 3 SMTP Email Delivery Validation / v4.6.2' not in roadmap:
    raise SystemExit('Current roadmap missing Stage 8G Package 2 entry')
print('Stage 8G Package 2 static validation passed.')

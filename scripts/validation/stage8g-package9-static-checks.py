from pathlib import Path
root = Path(__file__).resolve().parents[2]
required = [
    root / 'frontend/garmetix-web/pages/stage8g-completion/index.vue',
    root / 'scripts/linux/stage8g-final-acceptance-check.sh',
    root / 'scripts/linux/role-acceptance-matrix.sh',
    root / 'docs/operations/Role-Acceptance-Matrix-v4.6.8.md',
    root / 'docs/operations/Production-Release-Checklist-v4.6.8.md',
    root / 'docs/stages/stage-8/Stage8G-Package9-FinalGoLiveAcceptance-v4.6.8-Notes.md',
]
missing = [str(p.relative_to(root)) for p in required if not p.exists()]
if missing:
    raise SystemExit(f'Missing Stage 8G Package 9 files: {missing}')
version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
for token in ["APP_VERSION = '4.6.8'", 'Stage 8G Package 9 Final Go-Live Acceptance', 'GARMETIX-8G-20260617-4680']:
    if token not in version:
        raise SystemExit(f'Frontend app version token missing: {token}')
backend = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
for token in ['Version = "4.6.8"', 'Stage 8G Package 9 Final Go-Live Acceptance', 'GARMETIX-8G-20260617-4680']:
    if token not in backend:
        raise SystemExit(f'Backend app info token missing: {token}')
app_shell = (root / 'frontend/garmetix-web/components/AppShell.vue').read_text()
if '/stage8g-completion' not in app_shell:
    raise SystemExit('AppShell missing Stage 8G Completion route')
runtime = (root / 'backend/Garmetix.Api/Testing/TestAutomationEndpoints.cs').read_text()
if 'StartsWith("4.6."' not in runtime or 'StartsWith("GARMETIX-8G-"' not in runtime:
    raise SystemExit('Runtime smoke still has stale hard-coded Stage 8G version check')
roadmap = (root / 'docs/planning/CURRENT-ROADMAP.md').read_text()
if 'Stage 8G Package 9 Final Go-Live Acceptance / v4.6.8' not in roadmap:
    raise SystemExit('Current roadmap missing Stage 8G Package 9 entry')
print('Stage 8G Package 9 static validation passed.')

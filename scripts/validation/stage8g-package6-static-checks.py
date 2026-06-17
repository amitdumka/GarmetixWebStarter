from pathlib import Path
root = Path(__file__).resolve().parents[2]
required = [
    root / 'scripts/linux/oracle-cloud-readiness-check.sh',
    root / 'docs/stages/stage-8/Stage8G-Package6-OracleCloudSyncValidation-v4.6.5-Notes.md',
    root / 'docs/operations/Oracle-Cloud-Sync-Readiness-v4.6.5.md',
]
missing = [str(p.relative_to(root)) for p in required if not p.exists()]
if missing:
    raise SystemExit(f'Missing Stage 8G Package 6 files: {missing}')
frontend = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
backend = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
for token in ["APP_VERSION = '4.6.8'", 'Stage 8G Package 9 Final Go-Live Acceptance', 'GARMETIX-8G-20260617-4680']:
    if token not in frontend:
        raise SystemExit(f'Frontend version token missing: {token}')
for token in ['Version = "4.6.8"', 'Stage 8G Package 9 Final Go-Live Acceptance', 'GARMETIX-8G-20260617-4680']:
    if token not in backend:
        raise SystemExit(f'Backend version token missing: {token}')
script = (root / 'scripts/linux/oracle-cloud-readiness-check.sh').read_text()
for token in ['oracle-sync/cloud-readiness', 'oracle-sync/external-app-test-plan', 'oracle-sync/auto-apply-policy', 'ORACLE_SYNC_TNS_ADMIN', 'ORACLE_SYNC_WALLET_DIRECTORY']:
    if token not in script:
        raise SystemExit(f'Oracle readiness script missing token: {token}')
roadmap = (root / 'docs/planning/CURRENT-ROADMAP.md').read_text()
if 'Stage 8G Package 6 Oracle Cloud Sync Validation / v4.6.5' not in roadmap:
    raise SystemExit('Current roadmap missing Stage 8G Package 6 entry')
print('Stage 8G Package 6 static validation passed.')

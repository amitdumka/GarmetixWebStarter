from pathlib import Path
root = Path(__file__).resolve().parents[2]
required = [
    root / 'scripts/linux/production-security-hardening-check.sh',
    root / 'scripts/linux/log-retention-check.sh',
    root / 'docs/stages/stage-8/Stage8G-Package7-ProductionSecurityHardening-v4.6.6-Notes.md',
    root / 'docs/operations/Production-Security-Hardening-v4.6.6.md',
]
missing = [str(p.relative_to(root)) for p in required if not p.exists()]
if missing:
    raise SystemExit(f'Missing Stage 8G Package 7 files: {missing}')
frontend = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
backend = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
for token in ["APP_VERSION = '4.6.8'", 'Stage 8G Package 9 Final Go-Live Acceptance', 'GARMETIX-8G-20260617-4680']:
    if token not in frontend:
        raise SystemExit(f'Frontend version token missing: {token}')
for token in ['Version = "4.6.8"', 'Stage 8G Package 9 Final Go-Live Acceptance', 'GARMETIX-8G-20260617-4680']:
    if token not in backend:
        raise SystemExit(f'Backend version token missing: {token}')
security_script = (root / 'scripts/linux/production-security-hardening-check.sh').read_text()
for token in ['RESET_DATABASE_ON_DEPLOY', 'Strict-Transport-Security', 'DOCKER_LOG_MAX_SIZE', 'localhost-only', '/api/health']:
    if token not in security_script:
        raise SystemExit(f'Security script missing token: {token}')
log_script = (root / 'scripts/linux/log-retention-check.sh').read_text()
for token in ['LogConfig', 'DOCKER_LOG_MAX_SIZE', 'garmetix-api-1']:
    if token not in log_script:
        raise SystemExit(f'Log retention script missing token: {token}')
roadmap = (root / 'docs/planning/CURRENT-ROADMAP.md').read_text()
if 'Stage 8G Package 7 Production Security and Log Retention Hardening / v4.6.6' not in roadmap:
    raise SystemExit('Current roadmap missing Stage 8G Package 7 entry')
print('Stage 8G Package 7 static validation passed.')

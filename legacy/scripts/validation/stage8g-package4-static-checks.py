from pathlib import Path
root = Path(__file__).resolve().parents[2]
required = [
    root / 'scripts/linux/google-drive-backup-check.sh',
    root / 'scripts/linux/google-drive-upload-latest-backup.sh',
    root / 'docs/stages/stage-8/Stage8G-Package4-GoogleDriveBackupValidation-v4.6.3-Notes.md',
    root / 'docs/operations/Google-Drive-Backup-Validation-v4.6.3.md',
]
missing = [str(p.relative_to(root)) for p in required if not p.exists()]
if missing:
    raise SystemExit(f'Missing Stage 8G Package 4 files: {missing}')
service = (root / 'backend/Garmetix.Api/Backup/GoogleDriveBackupService.cs').read_text()
if 'var client = await CreateAuthorizedClientAsync(cancellationToken);\n            var client = await CreateAuthorizedClientAsync(cancellationToken);' in service:
    raise SystemExit('Google Drive service still has duplicate client declaration')
page = (root / 'frontend/garmetix-web/pages/backup-maintenance/index.vue').read_text()
for token in ['Google Drive off-site backup', 'backups/cloud/status', 'backups/cloud', 'Upload local backup', 'serviceAccountEmail']:
    if token not in page:
        raise SystemExit(f'Backup Maintenance cloud UI token missing: {token}')
version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
if "APP_VERSION = '4." not in version:
    raise SystemExit('Frontend version missing current Stage 8G version')
backend = (root / 'backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs').read_text()
if 'Version = "4.' not in backend:
    raise SystemExit('Backend version missing current Stage 8G version')
roadmap = (root / 'docs/planning/CURRENT-ROADMAP.md').read_text()
if 'Stage 8G Package 4 Google Drive Backup Validation / v4.6.3' not in roadmap:
    raise SystemExit('Current roadmap missing Stage 8G Package 4 entry')
print('Stage 8G Package 4 static validation passed.')

from pathlib import Path

root = Path(__file__).resolve().parents[2]
required = [
    root / 'frontend/garmetix-web/pages/backup-maintenance/index.vue',
    root / 'docs/stages/stage-8/Stage8G-Package1-BackupRestoreMaintenance-v4.6.0-Notes.md',
    root / 'docs/operations/Backup-Restore-Maintenance-v4.6.0.md',
    root / 'scripts/linux/backup-maintenance-check.sh',
    root / 'scripts/linux/create-database-backup-now.sh',
]
missing = [str(path.relative_to(root)) for path in required if not path.exists()]
if missing:
    raise SystemExit(f'Missing Stage 8G Package 1 files: {missing}')

backup_service = (root / 'backend/Garmetix.Api/Backup/DatabaseBackupService.cs').read_text()
for token in [
    'BackupMaintenanceStatusDto',
    'BackupCleanupResultDto',
    'BackupVerifyAllResultDto',
    'GetMaintenanceStatus',
    'CleanupBackupDirectoryAsync',
    'VerifyAllBackups',
    'CanWriteDirectory',
    'CountOrphanSidecars',
]:
    if token not in backup_service:
        raise SystemExit(f'DatabaseBackupService missing {token}')

backup_endpoints = (root / 'backend/Garmetix.Api/Backup/BackupEndpoints.cs').read_text()
for token in [
    '/maintenance/status',
    '/maintenance/cleanup',
    '/maintenance/verify-all',
    'MaintenanceStatusAsync',
    'MaintenanceCleanupAsync',
    'VerifyAllAsync',
]:
    if token not in backup_endpoints:
        raise SystemExit(f'BackupEndpoints missing {token}')

app_shell = (root / 'frontend/garmetix-web/components/AppShell.vue').read_text()
if '/backup-maintenance' not in app_shell or 'Backup Maintenance' not in app_shell:
    raise SystemExit('Sidebar missing Backup Maintenance link')

page = (root / 'frontend/garmetix-web/pages/backup-maintenance/index.vue').read_text()
for token in ['backups/maintenance/status', 'backups/maintenance/cleanup', 'backups/maintenance/verify-all', 'Create backup', 'Verify all', 'Cleanup']:
    if token not in page:
        raise SystemExit(f'Backup Maintenance page missing {token}')

version = (root / 'frontend/garmetix-web/utils/appVersion.ts').read_text()
if "APP_VERSION = '4." not in version:
    raise SystemExit('Frontend app version not compatible with current Stage 8G release')

csproj = (root / 'backend/Garmetix.Api/Garmetix.Api.csproj').read_text()
if '<Version>4.' not in csproj:
    raise SystemExit('Backend version not compatible with current Stage 8G release')

roadmap = (root / 'docs/planning/CURRENT-ROADMAP.md').read_text()
if 'Stage 8G Package 3 SMTP Email Delivery Validation / v4.6.2' not in roadmap:
    raise SystemExit('Current roadmap missing Stage 8G Package 3 entry')

print('Stage 8G Package 1 static validation passed.')

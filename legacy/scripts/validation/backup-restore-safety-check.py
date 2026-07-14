#!/usr/bin/env python3
from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
errors: list[str] = []

def read(rel: str) -> str:
    return (ROOT / rel).read_text(encoding='utf-8')

def exists(rel: str) -> bool:
    return (ROOT / rel).exists()

def add(name: str, ok: bool):
    if not ok:
        errors.append(name)

backup_service = read('backend/Garmetix.Api/Backup/DatabaseBackupService.cs')
backup_options = read('backend/Garmetix.Api/Backup/BackupOptions.cs')
backup_page = read('frontend/garmetix-web/pages/backup-maintenance/index.vue')
prod_readiness = read('backend/Garmetix.Api/Production/ProductionReadinessEndpoints.cs')
compose = read('docker-compose.prod.yml')
env_example = read('.env.production.example')
catalog = read('backend/Garmetix.Api/Testing/TestAutomationCatalog.cs')

add('backup retention days option added', 'RetentionDays' in backup_options and 'KeepMinimum' in backup_options)
add('backup restore marker option added', 'RestoreDrillMarkerPath' in backup_options)
add('docker backup retention env wired', 'Backup__RetentionDays' in compose and 'Backup__KeepMinimum' in compose and 'Backup__RestoreDrillMarkerPath' in compose)
add('env example backup retention policy', 'BACKUP_RETENTION_DAYS=30' in env_example and 'BACKUP_KEEP_MINIMUM=10' in env_example)
add('restore preflight checks required tables', 'RequiredRestoreTables' in backup_service and 'RequiredTablesMissing' in backup_service and 'RequiredTablesPresent' in backup_service)
add('backup manifest uses current app info', 'AppInfoEndpoints.Stage' in backup_service and 'AppInfoEndpoints.BuildCode' in backup_service)
add('backup maintenance exposes restore drill marker', 'LastRestoreDrillAtUtc' in backup_service and 'RestoreDrillStatus' in backup_service)
add('production readiness includes restore drill check', 'BACKUP_RESTORE_DRILL' in prod_readiness and 'backupService.GetMaintenanceStatus()' in prod_readiness)
add('backup page has restore preview', 'Restore Preview / Dry Run' in backup_page and 'previewLocalBackup' in backup_page and 'requiredTablesMissing' in backup_page)
add('linux backup restore drill exists', exists('scripts/linux/backup-restore-drill.sh'))
add('linux backup restore drill uses disposable db', 'garmetix_restore_drill_' in read('scripts/linux/backup-restore-drill.sh') and 'dropdb --if-exists' in read('scripts/linux/backup-restore-drill.sh'))
add('test automation manifest includes backup restore safety', 'BACKUP_RESTORE_SAFETY' in catalog and 'backup-restore-drill.sh' in catalog)

if errors:
    print('Backup/restore safety validation failed:', file=sys.stderr)
    for error in errors:
        print(f' - {error}', file=sys.stderr)
    sys.exit(1)

print('Backup/restore safety validation passed.')

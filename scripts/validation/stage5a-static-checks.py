#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]

checks = [
    (ROOT / 'backend/Garmetix.Api/Backup/DatabaseBackupService.cs', [
        'BackupVerificationDto',
        'RestorePreflightDto',
        'WriteChecksumSidecarAsync',
        'WriteManifestSidecarAsync',
        'pg_restore',
        '--list',
        'ComputeSha256',
    ]),
    (ROOT / 'backend/Garmetix.Api/Backup/BackupEndpoints.cs', [
        'restore/preview',
        '/{fileName}/verify',
        '/{fileName}/restore/preview',
    ]),
    (ROOT / 'frontend/garmetix-web/pages/system-health/index.vue', [
        'verifyBackup',
        'previewLocalRestore',
        'previewUploadedRestore',
        'restorePreview?.status !== \'ok\'',
        'integrityLabel',
    ]),
    (ROOT / 'scripts/linux/backup-db.sh', ['pg_dump --format=custom', 'sha256sum']),
    (ROOT / 'scripts/linux/restore-db.sh', ['pg_restore --list', 'Type RESTORE']),
    (ROOT / 'scripts/windows/backup-db.ps1', ['--format=custom', 'Get-FileHash']),
    (ROOT / 'scripts/windows/restore-db.ps1', ['pg_restore --list', 'Checksum verification failed']),
    (ROOT / 'docs/operations/release/Production-Release-Checklist.md', ['Restore drill', 'Data consistency check', 'Go-live rule']),
]

failures: list[str] = []
for path, needles in checks:
    if not path.exists():
        failures.append(f'Missing file: {path.relative_to(ROOT)}')
        continue
    text = path.read_text(encoding='utf-8')
    for needle in needles:
        if needle not in text:
            failures.append(f'Missing "{needle}" in {path.relative_to(ROOT)}')

# Lightweight brace balance for modified C# files.
for path in [
    ROOT / 'backend/Garmetix.Api/Backup/DatabaseBackupService.cs',
    ROOT / 'backend/Garmetix.Api/Backup/BackupEndpoints.cs',
]:
    text = path.read_text(encoding='utf-8')
    if text.count('{') != text.count('}'):
        failures.append(f'Brace count mismatch in {path.relative_to(ROOT)}')

if failures:
    print('Stage 5A static checks failed:')
    for failure in failures:
        print(f' - {failure}')
    sys.exit(1)

print('Stage 5A static checks passed.')

# Stage 11D-37 - Portable Backup Catalog and Naming (v4.11.52)

## Purpose

Backup files created by scripts or before redeploy were not always visible in the Backup Maintenance UI because the old scanner only listed top-level `garmetix-*.dump` files. This stage makes backups portable and admin-restorable across systems.

## Changes

- Backup Maintenance now scans the backup directory recursively for `*.dump` files.
- Backups copied into `/opt/garmetix/current/backups` or its subfolders appear after Refresh/redeploy.
- New backups use a portable filename format:

```text
CompanyName-Garmetix-vAppVersion-YYYYMMDD-HHMMSS-B001-source.dump
```

Example:

```text
AadwikaFashion-Garmetix-v4.11.52-20260625-214530-B001-manual.dump
```

- Backup manifest sidecars now include company name, app version, India local backup time, and sequence.
- Script backups now write `.dump.sha256` and `.dump.manifest.json` sidecars compatible with the app.
- Restore/preview/download can resolve copied backups by filename from the backup folder.

## Admin restore on another system

1. Copy `.dump`, `.dump.sha256`, and `.dump.manifest.json` into the target server backup folder.
2. Open **Backup Maintenance** as Admin.
3. Click **Refresh**.
4. Use **Preview restore** before restore.
5. Restore only after confirmation.

## Version

- Version: 4.11.52
- Stage: Stage 11D-37 Portable Backup Catalog and Naming
- Build: GARMETIX-11D37-20260625-4152

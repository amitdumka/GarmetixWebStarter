# Google Drive Online Backup

This project now supports optional Google Drive backup upload, list, download, delete, and restore using a Google service account.

## How it works

1. Garmetix first creates a normal local PostgreSQL custom-format `.dump` backup with `pg_dump`.
2. If Google Drive backup is enabled and `UploadOnBackup` is true, the backup is uploaded to the configured Drive folder.
3. The System Health page shows both local backups and Google Drive backups.
4. Admin users can download, delete, or restore a Google Drive backup. Restore still creates a local safety backup first.

## Backend endpoints

All endpoints require Admin authorization:

```text
GET    /api/backups/cloud/status
GET    /api/backups/cloud
POST   /api/backups/{fileName}/cloud
GET    /api/backups/cloud/{fileId}/download
DELETE /api/backups/cloud/{fileId}
POST   /api/backups/cloud/{fileId}/restore?confirmation=RESTORE
```

## Configuration

Use the production compose file and `.env` values:

```env
GOOGLE_DRIVE_BACKUP_ENABLED=true
GOOGLE_DRIVE_UPLOAD_ON_BACKUP=true
GOOGLE_DRIVE_FOLDER_ID=your-google-drive-folder-id
GOOGLE_DRIVE_RETENTION_COUNT=30
```

Create this file on the server:

```text
./secrets/google-drive-service-account.json
```

Docker mounts it read-only at:

```text
/app/secrets/google-drive-service-account.json
```

The compose file maps that path to:

```env
GoogleDriveBackup__ServiceAccountJsonPath=/app/secrets/google-drive-service-account.json
```

## Google setup steps

1. In Google Cloud Console, create a project.
2. Enable the Google Drive API.
3. Create a service account.
4. Create a JSON key for that service account.
5. Save the JSON key as `./secrets/google-drive-service-account.json` on your Garmetix server.
6. Create or choose a Google Drive folder for backups.
7. Share that folder with the service account `client_email` from the JSON file.
8. Copy the folder id from the Drive folder URL into `GOOGLE_DRIVE_FOLDER_ID`.
9. Restart the API container.

## Important notes

- The service uses a service-account JWT flow directly; no interactive browser OAuth is required on the server.
- Local backup success is not blocked if Google Drive upload fails. The local backup remains available.
- Only raw backup files with names like `garmetix-*.dump` are listed in the Google Drive backup table.
- Scheduled cloud retention removes old scheduled backups beyond `GOOGLE_DRIVE_RETENTION_COUNT`.
- Restore from Google Drive downloads the backup to a temporary server file, validates it as a PostgreSQL custom-format dump, creates a safety backup, then runs `pg_restore`.

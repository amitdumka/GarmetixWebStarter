# Stage 8G Package 4 - Google Drive Backup Validation (v4.6.3)

This package adds the off-site backup validation workflow required before go-live.

## Included

- Backup Maintenance now includes Google Drive off-site status, service-account identity, folder id, cloud backup count and upload action.
- Local backup rows can be uploaded to Google Drive from the Backup Maintenance page after verification.
- Added Mac mini helper scripts to validate Google Drive configuration and prepare the latest backup for upload.
- Fixed a Google Drive download compile-risk duplicate local variable.

## Required server files

- `secrets/google-drive-service-account.json`
- `GOOGLE_DRIVE_BACKUP_ENABLED=true`
- `GOOGLE_DRIVE_FOLDER_ID=<Drive folder id>`

Share the target Drive folder with the service account `client_email` before running the validation.

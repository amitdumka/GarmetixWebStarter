# Google Drive Backup Validation v4.6.3

Run on the Mac mini:

```bash
cd /opt/garmetix/current
./scripts/linux/google-drive-backup-check.sh .env.production
```

Then open **Maintenance > Backup Maintenance** and confirm:

1. Google Drive shows **Configured**.
2. Cloud backup list can load.
3. Create and verify a local backup.
4. Upload the verified backup to Google Drive.
5. Download/restore only into a disposable test database.

Keep the service-account JSON outside Git and ZIP sharing.

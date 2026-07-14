# Backup, Restore & Data Safety Verification v4.9.16

Stage 8I Package 17 adds a production recovery safety layer before more business modules are added.

## Added

- Backup retention now supports both count-based and day-based policy:
  - `BACKUP_RETENTION_COUNT`
  - `BACKUP_RETENTION_DAYS`
  - `BACKUP_KEEP_MINIMUM`
- Backup restore preview now checks:
  - PostgreSQL custom dump header
  - `pg_restore --list` readability
  - required Garmetix core table coverage
  - backup manifest application/stage
  - version mismatch warning
- Backup maintenance page now shows latest backup size, restore-drill status and retention policy.
- Production readiness includes `BACKUP_RESTORE_DRILL`.
- Host drill script added: `scripts/linux/backup-restore-drill.sh`.

## Safe restore drill

Run on the Docker host after deployment:

```bash
./scripts/linux/backup-restore-drill.sh .env.production
```

The script creates a dump, restores it into a temporary PostgreSQL database, validates required tables and drops the temporary database. It does not modify production data.

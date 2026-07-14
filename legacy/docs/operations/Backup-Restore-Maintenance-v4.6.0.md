# Garmetix v4.6.0 Backup Restore Maintenance

## Mac mini checks

```bash
cd /opt/garmetix/current
./scripts/linux/backup-maintenance-check.sh .env.production
```

## Create direct PostgreSQL backup

```bash
cd /opt/garmetix/current
./scripts/linux/create-database-backup-now.sh .env.production
```

## UI workflow

Open **Maintenance -> Backup Maintenance**. Use:

1. **Create backup** before deployments.
2. **Verify all** before restore or production cutover.
3. **Cleanup** after failed restore previews or interrupted uploads.

Do not keep `RESET_DATABASE_ON_DEPLOY=true` after first fresh install.

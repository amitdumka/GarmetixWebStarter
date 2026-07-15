# BS-21 - Restore Drill And Production Safety

Stage name: `BS21RestoreDrillProductionSafety`

Primary command:

```bash
npm --prefix frontend/modular run deploy:srp:restore-drill -- --confirm-non-production-restore --stage=BS21RestoreDrillProductionSafety
```

Dry-run:

```bash
npm --prefix frontend/modular run deploy:srp:restore-drill -- --dry-run
```

Use a specific backup file:

```bash
npm --prefix frontend/modular run deploy:srp:restore-drill -- --confirm-non-production-restore --stage=BS21RestoreDrillProductionSafety --backup-file=<backup-file.dump>
```

## Purpose

BS-21 proves that the stage backups under `/opt/garmetix/backup/database/` are restore-ready before any production accounting migration, ledger relink, live backfill, or report-source switch.

The restore drill is intentionally operational and does not add business-data mutation code.

## What The Restore Drill Does

The script `frontend/modular/deploy/srp-restore-drill.sh`:

- connects to the SRP host using the same deploy config as the backup/deploy scripts;
- reads the live database connection string from the remote API env file;
- finds the latest `.dump` in `/opt/garmetix/backup/database/` unless `--backup-file=` is supplied;
- confirms the backup exists in `Backupfilehistory.md`;
- verifies the `.sha256` checksum;
- runs `pg_restore -l` to verify the dump catalog can be read;
- restores into a non-production database named `garmetix_restore_drill_<stage>_<timestamp>`;
- refuses to restore into the live database name;
- runs SQL smoke against the restored database;
- starts a temporary API process against the restored database and calls `/api/health`;
- records restore metadata in `/opt/garmetix/backup/database/RestoreDrillHistory.md`;
- drops the restore-drill database after a successful smoke unless `--keep-restore-db` is supplied.

## Safety Gates

The script refuses a live restore unless:

- `--confirm-non-production-restore` is supplied;
- the restore database is not the production database;
- the restore database starts with `garmetix_restore_drill_`, unless `--allow-custom-restore-db` is explicitly supplied;
- a backup checksum file exists and passes;
- `Backupfilehistory.md` references the selected backup.

## Application Smoke

The temporary API smoke uses:

```text
GET /api/health
```

This route is anonymous and checks database connectivity. If the API binary is missing under `/opt/garmetix-srp/current/api`, the script fails unless the operator reruns with:

```bash
--skip-app-smoke
```

Use `--skip-app-smoke` only when the API artifact is intentionally unavailable and record that exception in the stage evidence.

## Rollback Decision Tree

1. If checksum verification fails, do not deploy or mutate accounting data. Create a fresh backup and investigate storage corruption.
2. If `pg_restore -l` fails, treat the dump as unusable. Do not use it as a rollback point.
3. If restore into non-production fails, do not start COA/party/ledger/backfill mutations. Fix PostgreSQL permissions or dump compatibility first.
4. If SQL smoke fails, inspect the restored database before any live accounting migration.
5. If temporary `/api/health` fails, inspect the API log from the restore drill and do not switch report sources or run live backfill.
6. If every check passes, the backup is acceptable as a rollback point for the next approved production accounting migration stage.

## Remote Handoff

After this code is pulled on the deployed host:

```bash
git pull origin version6
npm --prefix frontend/modular run deploy:srp:restore-drill -- --confirm-non-production-restore --stage=BS21RestoreDrillProductionSafety
```

Then capture:

- selected backup filename;
- checksum result;
- restore database name;
- SQL table count;
- `/api/health` smoke result;
- `RestoreDrillHistory.md` row.

Do not run production restore, accounting migration, ledger relink, live backfill, or report-source switch until Amit approves the restore drill evidence.

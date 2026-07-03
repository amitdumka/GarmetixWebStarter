# Stage 11D-36 Admin JSON Data Maintenance - v4.11.51

This stage adds an Admin-only JSON export/import and destructive data maintenance center.

## UI page

`Data -> Admin JSON Data`

## Features

- Select one or more allowed tables and export JSON.
- Validate JSON package before import.
- Import JSON in `upsert`, `insert-only`, or `replace-table` mode.
- Before every JSON import, the API creates an `AdminJsonBackups` snapshot containing selected tables.
- List and restore JSON backup snapshots.
- Cascade-delete selected rows by UUID.
- Clear a full table with database cascade.
- Before every destructive operation, a JSON snapshot backup is created.
- Admin-only authorization.

## Safety confirmations

- Import: `IMPORT JSON`
- Restore JSON snapshot: `RESTORE JSON BACKUP`
- Delete selected rows with cascade: `DELETE WITH CASCADE`
- Clear table data: `DELETE TABLE DATA`

## Full database backup scripts

The UI creates table-level JSON snapshots. For full disaster rollback, run this before large operations:

```bash
cd /opt/garmetix/current
./scripts/production/stage11d36-admin-data-full-db-backup.sh
```

Restore full DB backup:

```bash
./scripts/production/stage11d36-restore-admin-data-full-db-backup.sh /opt/garmetix/current /path/to/backup.dump
```

## Intended use

This feature is for early production cleanup only, where imported data may need destructive correction. Do not expose this page to normal users.

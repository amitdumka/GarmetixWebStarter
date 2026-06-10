# Stage 5A — Backup/Restore Hardening + Release Readiness

## Goal

Stage 5A makes production recovery safer before the app is used for live store data. The previous stages already added business hardening, GST reports, data consistency checks, and controlled repairs. This stage focuses on backup integrity, restore preflight, operator guidance, and release verification.

## Backend changes

### Backup metadata

Every new database backup created through the API now writes sidecar files:

- `<backup>.sha256` — SHA256 checksum for corruption/tamper detection.
- `<backup>.manifest.json` — source, database, host, port, size, timestamp, format, application, and stage metadata.

Existing legacy backups still appear in the UI. They are marked as legacy if checksum/manifest files do not exist.

### New backup verification API

Added:

```text
GET /api/backups/{fileName}/verify
```

This checks:

- file exists under the configured backup directory only;
- file has the PostgreSQL custom dump `PGDMP` header;
- checksum sidecar exists;
- checksum matches the actual backup bytes;
- manifest sidecar exists.

### New restore preflight API

Added:

```text
POST /api/backups/restore/preview
POST /api/backups/{fileName}/restore/preview
```

The preflight checks the PostgreSQL custom dump header and runs:

```bash
pg_restore --list <backup.dump>
```

The UI now shows the first dump TOC lines before enabling uploaded-file restore.

### Restore safety

Restore still requires the exact confirmation text:

```text
RESTORE
```

Before restore, the API still creates a pre-restore safety backup. Stage 5A additionally runs `pg_restore --list` before restore so unreadable dumps fail before database replacement begins.

## Frontend changes

The System Health page now shows local backup integrity state:

- Checksum + manifest
- Checksum only
- Legacy file

Added local backup actions:

- Verify
- Preflight

The upload-restore modal now has a required preflight step. The final Restore button stays disabled until:

1. a file is selected;
2. preflight succeeds;
3. the operator types `RESTORE`.

## Script changes

Added Linux/Mac scripts under:

```text
scripts/linux/
```

- `backup-db.sh`
- `restore-db.sh`
- `health-check.sh`
- `release-preflight.sh`

Updated Windows scripts to use PostgreSQL custom-format dumps instead of plain SQL:

- `scripts/windows/backup-db.ps1`
- `scripts/windows/restore-db.ps1`

Both Windows and Linux/Mac scripts now generate or verify SHA256 sidecars.

## Release checklist

Added:

```text
Production-Release-Checklist.md
```

Use it before moving a store to live usage.

## Local validation performed in this sandbox

- ZIP extraction from Stage 4E succeeded.
- Stage 5A static checks passed.
- Python syntax validation for validation script passed.
- ZIP integrity test passed.

## Not run in this sandbox

- `dotnet build` — .NET SDK is not installed here.
- Docker build — Docker is not available here.
- Full Nuxt build — external font/icon provider DNS is unreliable in this sandbox.

Run these locally:

```bash
cd backend
dotnet build

cd ../frontend/garmetix-web
npm install
npm run build

cd ../..
docker compose up --build
```

Then open:

```text
/system-health
```

Create a backup, verify it, run preflight, and only then test restore on a non-production copy.

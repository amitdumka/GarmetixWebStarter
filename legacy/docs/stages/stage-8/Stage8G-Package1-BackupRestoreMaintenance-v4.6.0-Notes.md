# Stage 8G Package 1 - Backup Restore Maintenance (v4.6.0)

This package starts Stage 8G by turning backup/restore from a system-health subsection into an explicit production maintenance workflow.

## Added

- Backup Maintenance Center in the sidebar under Maintenance.
- API endpoints for backup maintenance status, cleanup and verify-all operations.
- Local backup status now includes directory write access, free disk space, backup folder size, recent-backup age, checksum/manifest coverage, orphan sidecars and stale restore temp files.
- Cleanup removes orphan checksum/manifest files and stale restore upload/preview dumps without deleting valid backup dumps.
- Verify-all checks every local backup file and reports pass/fail counts.
- Linux/WSL helper scripts for Mac mini backup maintenance and direct PostgreSQL backup creation.

## Operational note

Use `RESET_DATABASE_ON_DEPLOY=false` for all normal redeployments. Run a manual backup before any upgrade, restore, or database-reset operation.

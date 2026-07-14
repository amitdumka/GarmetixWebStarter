# Stage 11D-33 Backup Automation and Restore Drill

Version: **4.11.48**  
Build: `GARMETIX-11D33-20260625-4148`

This stage adds production backup automation and a safer restore drill workflow.

## Scripts

- `scripts/production/stage11d33-backup-now.sh`
- `scripts/production/stage11d33-install-nightly-backup-cron.sh`
- `scripts/production/stage11d33-restore-drill-latest.sh`

## Purpose

Before final import and go-live, verify that PostgreSQL backup files can be created and restored into a drill database without touching the live database.

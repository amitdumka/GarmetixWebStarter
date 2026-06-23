# Go-Live Readiness v4.6.1

Run on the Mac mini after deployment:

```bash
cd /opt/garmetix/current
./scripts/linux/go-live-readiness-check.sh .env.production
./scripts/linux/run-backup-restore-drill.sh .env.production
```

## What to verify

- Docker containers are running.
- API and web health endpoints return HTTP 200.
- `/api/production-readiness/summary` and `/api/production-readiness/checklist` return data.
- `/api/backups/maintenance/status` reports a usable backup directory and recent backup status.
- Public domain responds over HTTPS.

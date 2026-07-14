# Post-Go-Live Acceptance v4.7.0

Run after deployment:

```bash
cd /opt/garmetix/current
./deploy/diagnose-production.sh
./scripts/linux/post-go-live-acceptance-check.sh .env.production
```

Then log in with representative roles and complete the manual checklist shown by the script.

## UI page

Open:

```text
Maintenance → Post-Go-Live Acceptance
```

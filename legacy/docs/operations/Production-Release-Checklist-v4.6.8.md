# Production Release Checklist - v4.6.8

Run after deployment on the Mac mini from `/opt/garmetix/current`:

```bash
./deploy/diagnose-production.sh
./scripts/linux/stage8g-final-acceptance-check.sh .env.production
./scripts/linux/role-acceptance-matrix.sh
```

Before sign-off, confirm:

- `RESET_DATABASE_ON_DEPLOY=false`.
- Docker containers are running and restart automatically.
- Public HTTPS domain works.
- API, web proxy, app-info and runtime smoke endpoints pass.
- Recent verified local backup exists.
- Off-site backup readiness is checked or deliberately deferred.
- SMTP, GSTIN and Oracle sync readiness are checked or deliberately deferred with notes.
- Role acceptance matrix is verified using real users.

# Stage 8G Package 7 - Production Security and Log Retention Hardening (v4.6.6)

This package adds production security verification around the Mac mini Docker/Cloudflare deployment.

## Included

- Added `scripts/linux/production-security-hardening-check.sh` for live security readiness checks.
- Added `scripts/linux/log-retention-check.sh` for Docker json-file log rotation verification.
- Hardened `scripts/linux/production-preflight.sh` with CRLF-safe env loading, database-reset protection and log-retention checks.
- Documented HTTPS headers, localhost-only port binding, strong secret checks, Docker auto-start and log-retention expectations.
- Preserved `RESET_DATABASE_ON_DEPLOY=false` as the deployment default.

## Acceptance

Run after deployment:

```bash
cd /opt/garmetix/current
./scripts/linux/production-security-hardening-check.sh .env.production
./scripts/linux/log-retention-check.sh .env.production
```

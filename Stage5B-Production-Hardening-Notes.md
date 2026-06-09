# Stage 5B — Production Environment Hardening Notes

Package: `Garmetix-Stage5B-ProductionHardening-v1.5.zip`

Base package: Stage 5A Backup/Restore Release v1.4

## Goal

Harden the app for controlled production deployment on Linux or Mac mini using Docker Compose. This stage focuses on secrets, HTTPS/tunnel readiness, logging, monitoring, production preflight checks, and safe update/rollback workflow.

## Backend changes

### Production readiness API

Added a new admin-only backend module:

- `GET /api/production-readiness/summary`
- `GET /api/production-readiness/checklist`

Files added:

- `backend/Garmetix.Api/Production/ProductionReadinessDtos.cs`
- `backend/Garmetix.Api/Production/ProductionReadinessEndpoints.cs`

The readiness summary checks:

- ASP.NET Core environment
- JWT signing key strength
- database password/default secret risk
- CORS production origins
- public HTTPS frontend URL
- SMTP/password-reset readiness
- local backup readiness
- off-site/cloud backup status
- Oracle auto-apply safety
- forwarded-header support for reverse proxy/tunnel deployments
- production log level

### Configurable CORS

`Program.cs` no longer hardcodes only `http://localhost:3000`. It now reads:

- `Cors:AllowedOrigins`
- `Cors:AllowedOriginsCsv`
- `CORS_ALLOWED_ORIGINS` through Docker Compose mapping

If no origin is configured, it safely falls back to localhost for development.

### Security response headers

Added API middleware for:

- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Permissions-Policy`
- HSTS when request is HTTPS

## Frontend changes

Added admin page:

- `/production-readiness`

Added sidebar menu item:

- **Admin → Production Readiness**

The page shows:

- overall status: Ready / Needs attention / Blocked
- passed/warning/critical counts
- detailed readiness checks
- fix hints for each issue
- go-live checklist
- useful production commands

## Docker Compose hardening

Updated `docker-compose.prod.yml` with:

- `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`
- `Cors__AllowedOriginsCsv`
- `NUXT_PUBLIC_SITE_URL`
- Docker JSON log rotation for API, web, and PostgreSQL

## Environment files

Updated `.env.example` and added:

- `.env.production.example`

The production example separates live deployment settings from development defaults and includes placeholders for:

- public HTTPS URL
- CORS origin
- SMTP
- backup retention
- Google Drive backup
- Oracle sync controls
- Docker log rotation

## Linux/Mac production scripts

Added:

- `scripts/linux/generate-secrets.sh`
- `scripts/linux/production-preflight.sh`
- `scripts/linux/monitor-health.sh`
- `scripts/linux/tail-logs.sh`
- `scripts/linux/deploy-release.sh`
- `scripts/linux/rollback-release.sh`
- `scripts/linux/start-cloudflare-tunnel.sh`

These scripts cover secret generation, preflight validation, health monitoring, log viewing, deployment, rollback, and Cloudflare Tunnel startup.

## HTTPS / tunnel examples

Added:

- `infra/cloudflare/config.example.yml`
- `infra/caddy/Caddyfile.example`

Cloudflare Tunnel is recommended when the store has dynamic IP or router limitations. Caddy is a simple option for a fixed server/domain.

## Auto-start example

Added:

- `deploy/systemd/garmetix.service`

This can be installed on Linux to start the Docker Compose stack after reboot.

## Documentation

Added:

- `Production-Environment-Hardening.md`

Updated:

- `Production-Release-Checklist.md`

## Recommended first-run production flow

```bash
cp .env.production.example .env.production
scripts/linux/generate-secrets.sh .env.production
nano .env.production
scripts/linux/production-preflight.sh .env.production
docker compose --env-file .env.production -f docker-compose.prod.yml up -d --build
scripts/linux/monitor-health.sh .env.production
```

Then open:

```text
/production-readiness
/system-health
/data-consistency
```

## Important notes

- Do not commit the real `.env.production` file.
- Do not commit service account JSON files.
- Keep off-site backup enabled before production billing starts.
- Keep Oracle inbound auto-apply disabled until trusted sources and ownership rules are approved.
- Use the update/rollback scripts after the first stable deployment folder structure is created.

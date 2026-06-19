# Docker Acceptance Drill v4.9.14

## Purpose

Stage 8I Package 15 adds a repeatable production-host drill for the next go-live checkpoint. It does not mark the real production run complete; it gives the Mac mini, WSL or Linux host one command that verifies the deployment path before live billing.

## Added scripts

```bash
scripts/linux/docker-acceptance-drill.sh
scripts/windows/docker-acceptance-drill.ps1
scripts/validation/frontend-route-access-check.py
scripts/validation/stage8i-package15-static-checks.py
```

## What the Linux drill checks

`./scripts/linux/docker-acceptance-drill.sh .env.production` performs:

1. Static Nuxt route-access audit.
2. Docker Compose production image build.
3. Docker Compose production startup.
4. API `/api/health` wait loop.
5. Nuxt web-root response check.
6. Web proxy `/api/app-info/version` version/build check.
7. Standard API smoke test.
8. Authenticated dashboard/workspace/readiness acceptance when `GARMETIX_SMOKE_USER` and `GARMETIX_SMOKE_PASSWORD` are supplied.
9. Final container status output.

## Authenticated acceptance endpoints

When smoke credentials are available, the drill validates:

```text
POST /api/auth/login
GET  /api/dashboard/home
GET  /api/workspace/options
GET  /api/setup/status
GET  /api/release-stabilization/smoke-checks
GET  /api/production-readiness/summary
```

## Route-access audit

The route-access audit scans `frontend/garmetix-web/pages` and confirms each concrete page has an explicit `routeRules` entry in `useAccessControl.ts`, except for approved special routes:

```text
/access-denied
/[module]
```

This prevents future pages from silently falling through the permissive unknown-route branch.

## Test Automation manifest

The release manifest now includes:

```text
FRONTEND_ROUTE_ACCESS_AUDIT
DOCKER_ACCEPTANCE_DRILL
```

The Linux and Windows smoke scripts require both manifest codes, so stale APIs or mixed release files fail early.

## Recommended host command

```bash
export GARMETIX_SMOKE_USER='admin'
export GARMETIX_SMOKE_PASSWORD='your-admin-password'
./scripts/linux/docker-acceptance-drill.sh .env.production
```

For Windows PowerShell:

```powershell
$env:GARMETIX_SMOKE_USER = 'admin'
$env:GARMETIX_SMOKE_PASSWORD = 'your-admin-password'
scripts/windows/docker-acceptance-drill.ps1 -EnvFile .env.production
```

## Not completed by this package

The package cannot prove the live host passed. After deploying the ZIP, run the drill on the Mac mini/WSL/Linux host and keep the output as the acceptance record.

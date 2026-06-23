# Stage 10H Runtime Bug Fix Pack v4.10.15

This package adds a runtime diagnostics page and API so the deployment host can quickly identify schema drift, failed table probes and common production configuration warnings after Docker restart.

## Added

- `/runtime-diagnostics` admin page.
- `/api/runtime-diagnostics` summary API.
- `/api/runtime-diagnostics/page-contracts` page/API map.
- `/api/runtime-diagnostics/known-runtime-checks` manual smoke checklist.
- `scripts/linux/stage10h-runtime-diagnostics-drill.sh` host acceptance script.
- Static validation for Stage 10H runtime diagnostics.

## Purpose

The goal is not to add a new business module. This package makes it easier to find runtime problems after installing the broad Stage 8/9/10 feature set.

## Recommended test

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml build
docker compose --env-file .env.production -f docker-compose.prod.yml up -d --force-recreate
export GARMETIX_SMOKE_USER='admin'
export GARMETIX_SMOKE_PASSWORD='your-admin-password'
./scripts/linux/stage10h-runtime-diagnostics-drill.sh .env.production
```

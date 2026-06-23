# Garmetix v4.5.1 Automated Smoke Tests

Use these commands after deployment or before packaging a release.

## Local source checks

```bash
./scripts/linux/run-automated-tests.sh
```

This runs backend xUnit tests, Nuxt build, Stage 8F Package 2 static validation and current-release validation.

## Mac mini deployed container checks

```bash
cd /opt/garmetix/current
RUN_BACKEND_TESTS=false RUN_FRONTEND_BUILD=false RUN_DOCKER_SMOKE=true ./scripts/linux/run-automated-tests.sh
```

This verifies running Docker containers, local API health, Nuxt proxy health, web root branding and `/api/test-automation/runtime-smoke`.

## Public frontend check through Cloudflare

```bash
cd frontend/garmetix-web
GARMETIX_WEB_BASE_URL=https://garmetix.aadwikafashion.in npm run smoke:frontend
```

## Optional authenticated checks

```bash
GARMETIX_SMOKE_USER=admin GARMETIX_SMOKE_PASSWORD='your-password' ./scripts/linux/smoke-test.sh .env.production
```

The authenticated checks verify login, release smoke summary and production readiness summary.

## Windows PowerShell

```powershell
.\scripts\windows\run-automated-tests.ps1
```

For a deployed API:

```powershell
$env:API_BASE_URL='https://garmetix.aadwikafashion.in/api'
.\scripts\windows\smoke-test.ps1
```

# Stage 8F Package 2 - Automated Smoke Tests (v4.5.1)

This package continues Stage 8F after persistent audit history by adding repeatable backend, frontend and production deployment smoke tests.

## Added

- Test automation manifest endpoint: `GET /api/test-automation/manifest`.
- Runtime smoke endpoint: `GET /api/test-automation/runtime-smoke`.
- Backend xUnit contract tests for the test automation catalog.
- Linux/WSL automation runner: `scripts/linux/run-automated-tests.sh`.
- Linux Docker health smoke: `scripts/linux/docker-smoke-test.sh`.
- Hardened Linux API smoke script: `scripts/linux/smoke-test.sh`.
- Windows automation runner: `scripts/windows/run-automated-tests.ps1`.
- Hardened Windows API smoke script: `scripts/windows/smoke-test.ps1`.
- Browserless frontend smoke script: `frontend/garmetix-web/scripts/frontend-smoke.mjs`.
- NPM command: `npm run smoke:frontend`.

## Suggested test commands

```bash
./scripts/linux/run-automated-tests.sh
```

For deployed Mac mini health checks:

```bash
RUN_BACKEND_TESTS=false RUN_FRONTEND_BUILD=false RUN_DOCKER_SMOKE=true ./scripts/linux/run-automated-tests.sh
```

For public frontend smoke:

```bash
cd frontend/garmetix-web
GARMETIX_WEB_BASE_URL=https://garmetix.aadwikafashion.in npm run smoke:frontend
```

## Notes

- Authenticated API smoke checks are optional and run only when `GARMETIX_SMOKE_USER` and `GARMETIX_SMOKE_PASSWORD` are set.
- The runtime smoke endpoint does not expose secrets; it checks only application version, PostgreSQL connectivity, audit table availability and the test manifest contract.
- `RESET_DATABASE_ON_DEPLOY=false` remains the safe deployment default.

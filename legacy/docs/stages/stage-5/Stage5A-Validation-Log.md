# Stage 5A Validation Log

## Completed in sandbox

- Extracted Stage 4E package successfully.
- Added backend backup checksum, manifest, verification, and restore-preflight code.
- Added System Health UI verification/preflight controls.
- Added Linux/Mac backup, restore, health check, and release preflight scripts.
- Updated Windows backup/restore scripts to use PostgreSQL custom-format dumps and SHA256 validation.
- Added production release checklist.
- Ran `scripts/validation/stage5a-static-checks.py` successfully.
- Ran Vue SFC parse/template validation for `pages/system-health/index.vue` successfully after installing frontend dependencies.
- ZIP integrity test passed after packaging.

## Build limitations

- `.NET SDK` is not installed in this sandbox, so `dotnet build` could not be run here.
- Docker is not available in this sandbox, so Docker build could not be run here.
- `npm run build` was attempted but timed out because external font/icon provider DNS calls failed from the sandbox.

## Required local validation

```bash
cd backend
dotnet restore
dotnet build --configuration Release

cd ../frontend/garmetix-web
npm ci
npm run build

cd ../..
docker compose up --build
```

Then login as admin and test:

1. Open `/system-health`.
2. Create a backup.
3. Click Verify.
4. Click Preflight.
5. Test uploaded restore preflight on a copied/test backup.
6. Perform a restore drill on a non-production database only.

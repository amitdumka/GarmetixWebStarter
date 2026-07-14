# Stage 5C Validation Log

Validation performed in this sandbox:

- Stage 5C static file/registration checks passed.
- Linux shell smoke-test script passed `bash -n`.
- C# release endpoint brace-balance check passed.
- Vue release page SFC parse/template compile passed.
- `npm ci --ignore-scripts` completed; npm warned that the current Node `v22.16.0` is below some package engine ranges (`^22.18.0 || >=24.11.0`), but dependencies installed and audit found 0 vulnerabilities.
- ZIP integrity check passed after packaging.

Not run in this sandbox:

- `dotnet build`, because .NET SDK is not installed here.
- Docker build, because Docker is not available here.
- Full Nuxt production build, because earlier builds in this environment timed out while resolving external font/icon provider metadata.

Run locally:

```bash
cd backend
dotnet build

cd ../frontend/garmetix-web
npm ci
npm run build

cd ../..
docker compose up --build
```

Smoke test after deployment:

```bash
scripts/linux/smoke-test.sh .env.production
GARMETIX_SMOKE_USER=admin GARMETIX_SMOKE_PASSWORD='password' scripts/linux/smoke-test.sh .env.production
```

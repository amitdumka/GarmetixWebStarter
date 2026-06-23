# Stage 01 - Version5 Modular Foundation

## What Changed

- Created `legacy/` as the intact Version4 project context.
- Created `modular/` as the new frontend workspace.
- Added six frontend app shells: main, POS, HR, AI Sense, Books and Admin/SaaS.
- Added shared package boundaries for API, auth, types and UI metadata.
- Kept the backend API and PostgreSQL database unsplit.

## Migration Approach

Move screens from `legacy/frontend/garmetix-web/pages` into modular apps module by module:

1. Extract shared API calls into `modular/packages/shared-api`.
2. Extract shared auth/token/permission checks into `modular/packages/shared-auth`.
3. Move only the route pages needed by each app.
4. Keep route guards server-compatible with the existing JWT and role matrix.
5. Build each app as a static SPA/PWA target behind Cloudflare Tunnel.

## First Candidate Modules

- POS: sale invoice, product lookup, customer lookup, day begin/end.
- HR: employees, attendance, monthly attendance, payroll review.
- Books: ledgers, vouchers, bank transactions, GST returns.
- Admin: license, company setup, users, roles, system status.
- AI Sense: today's dashboard, reports, analytics summary.

## Validation

```powershell
node modular/scripts/validate-structure.mjs
dotnet build legacy/backend/Garmetix.Api/Garmetix.Api.csproj -c Release
dotnet test legacy/backend/Garmetix.Api.Tests/Garmetix.Api.Tests.csproj -c Release
Push-Location legacy/frontend/garmetix-web; npm.cmd run build; Pop-Location
Push-Location modular; npm.cmd install --no-audit --fund=false --prefer-offline --loglevel=warn --strict-ssl=false; Pop-Location
Push-Location modular; npm.cmd run build:main; Pop-Location
Push-Location modular; npm.cmd run build:pos; Pop-Location
Push-Location modular; npm.cmd run build:hr; Pop-Location
Push-Location modular; npm.cmd run build:ai-sense; Pop-Location
Push-Location modular; npm.cmd run build:books; Pop-Location
Push-Location modular; npm.cmd run build:admin; Pop-Location
```

## Verification Notes

- Backend build passed with the existing nullable warning in `BillingEndpoints.cs`.
- Backend tests passed: 72 passed, 3 PostgreSQL concurrency tests skipped.
- Legacy Nuxt app built successfully after hydrating dependencies with `npm ci`.
- All six modular app shells generated static output under their local `.output/public` folders.
- Nuxt emitted non-fatal font provider certificate warnings on this Windows machine. The generated static apps still completed successfully.

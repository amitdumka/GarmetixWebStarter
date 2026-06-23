# GarmetixWebStarter Version5

Version5 is the modular frontend workspace. The existing working application has been moved intact to `legacy/` so it remains the source of truth while smaller frontends are created under `modular/`.

## Branch Purpose

- `version4`: legacy route for the current single frontend and API layout.
- `Version5`: modular route for the new multi-frontend structure.

## Folder Layout

- `legacy/`: current GarmetixWebStarter project exactly as the Version4 codebase context.
- `modular/`: new frontend workspace for smaller role-focused apps.

## Target Frontends

- `modular/apps/main`: back office app for full operations.
- `modular/apps/pos`: POS app for billing-counter users.
- `modular/apps/hr`: HR and attendance app.
- `modular/apps/ai-sense`: analytics and AI Sense app.
- `modular/apps/books`: accounting, books, accountant and CA app.
- `modular/apps/admin`: Admin/SaaS developer-owner app.

All modular apps use the same ASP.NET Core API, PostgreSQL database, auth token, workspace, and role/permission model.

## Commands

Legacy validation:

```powershell
dotnet test legacy/backend/Garmetix.Api.Tests/Garmetix.Api.Tests.csproj -c Release
Push-Location legacy/frontend/garmetix-web; npm.cmd run build; Pop-Location
```

Modular structure validation:

```powershell
node modular/scripts/validate-structure.mjs
```

## Environment

Use environment variables for domains and API URLs. Do not hardcode production domains inside app code.

See `modular/.env.example` and `modular/docs/cloudflare-subdomains.md`.


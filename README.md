# GarmetixWebStarter Version6

Version6 keeps one shared backend and separates frontend work into legacy and modular areas. New frontend development belongs in `frontend/modular/`; the legacy frontend in `frontend/legacy/` stays unchanged unless a task explicitly asks for a legacy frontend change.

## Branch Purpose

- `version4`: legacy route for the older single frontend and API layout.
- `version6`: current base for the shared backend plus modular frontend structure.

## Folder Layout

- `backend/`: shared ASP.NET Core API, domain, infrastructure, and tests.
- `frontend/legacy/garmetix-web/`: legacy Nuxt frontend for `garmetix.aadwikafashion.in`.
- `frontend/modular/`: modular Nuxt workspace for SRP and future role-focused apps.
- `legacy/`: legacy deployment notes, release notes, and support files retained for reference.

## Target Frontends

- `frontend/modular/apps/main`: back office app for full operations.
- `frontend/modular/apps/pos`: POS app for billing-counter users.
- `frontend/modular/apps/hr`: HR and attendance app.
- `frontend/modular/apps/ai-sense`: analytics and AI Sense app.
- `frontend/modular/apps/books`: accounting, books, accountant and CA app.
- `frontend/modular/apps/admin`: Admin/SaaS developer-owner app.

All modular apps use the same ASP.NET Core API, PostgreSQL database, auth token, workspace, and role/permission model.

## Commands

Legacy validation:

```powershell
dotnet test backend/Garmetix.Api.Tests/Garmetix.Api.Tests.csproj -c Release
Push-Location frontend/legacy/garmetix-web; npm.cmd run build; Pop-Location
```

Modular structure validation:

```powershell
node frontend/modular/scripts/validate-structure.mjs
```

## Environment

Use environment variables for domains and API URLs. Do not hardcode production domains inside app code.

See `frontend/modular/.env.example` and `frontend/modular/docs/cloudflare-subdomains.md`.

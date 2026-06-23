# Stage 12A.2 Frontend Workspace Foundation

## Purpose

This stage creates a safe, additive foundation for decoupling Garmetix into smaller frontend apps and shared frontend packages inside the `modular/` workspace.

No existing route is moved or deleted in this stage. The current working Nuxt app remains in `legacy/frontend/garmetix-web` as the Version4/Stage 11 reference and production-safe fallback until each modular app reaches parity.

All Version5 Stage 12 onward frontend work belongs under `modular/`. Extra prompts and external roadmap notes should be used as guidance, then adapted into the existing modular structure instead of creating new top-level app/package folders.

## Current Safe Layout

| Path | Role |
| --- | --- |
| `legacy/frontend/garmetix-web` | Current working Nuxt app. Treat this as `main-web` for now. |
| `legacy/backend/Garmetix.Api` | Current ASP.NET Core API. Not split in Stage 12. |
| `legacy/backend/Garmetix.Infrastructure` | Current EF Core/PostgreSQL infrastructure. Not split in Stage 12. |
| `modular/apps/*` | Stage 12A generated Nuxt static app shells. These are build-active inside `modular/package.json`. |
| `modular/packages/*` | Shared frontend package contracts used by the modular app shells. |
| `modular/docs/*` | Stage 12 onward roadmap, migration notes, and modular frontend documentation. |

## Future App Folders

| Folder | Intended app | Current status |
| --- | --- | --- |
| `modular/apps/main` | Main/back-office app | Shell app for the future main/back-office modular UI. Legacy remains fallback for now. |
| `modular/apps/pos` | POS app | Focused POS/store operator app. |
| `modular/apps/hr` | HR app | HR, attendance, payroll app. |
| `modular/apps/ai-sense` | AI Sense analytics app | Analytics/read-only insight app. |
| `modular/apps/books` | Accounting app | Accountant/CA books, vouchers, GST, audit workflows. |
| `modular/apps/admin` | Admin/SaaS app | Owner/developer/admin control app. |

## Shared Package Folders

| Folder | Purpose |
| --- | --- |
| `modular/packages/shared-api` | Shared API URL, request, error, and typed endpoint helpers. |
| `modular/packages/shared-auth` | Shared token/session/user/role helpers. |
| `modular/packages/shared-types` | Shared DTO, route, menu, user, and app metadata types. |
| `modular/packages/shared-utils` | Shared date, money, string, validation, and formatting helpers. |
| `modular/packages/shared-ui` | Shared UI contracts and later reusable Vue/Nuxt UI components. |

## Package Manager Decision

The repo uses npm:

- `legacy/frontend/garmetix-web/package-lock.json`
- `modular/package-lock.json`

No pnpm, yarn, or bun workspace is introduced. Root `package.json` is not converted into an npm workspace in this stage. That avoids changing install resolution for the existing legacy app and backend validation scripts.

The existing `modular/package.json` is the npm workspace for Version5 frontend apps and packages. Root `package.json` stays only as a convenience command runner for validation.

## How These Folders Will Be Used

1. Stage 12B will extract auth/session primitives from `legacy/frontend/garmetix-web/composables/useAuth.ts` into `modular/packages/shared-auth`.
2. Stage 12B will extract request primitives from `legacy/frontend/garmetix-web/composables/useGarmetixApi.ts` into `modular/packages/shared-api`.
3. Route/menu/permission types from `AppShell.vue` and `useAccessControl.ts` will be normalized into `modular/packages/shared-types`.
4. Date/money formatting and safe message formatting will move into `modular/packages/shared-utils`.
5. Only after shared packages compile cleanly will app-specific pages be copied or adapted into app folders.

## Rules For Next Stages

- Keep the legacy app building after every extraction.
- Do not delete any route from `legacy/frontend/garmetix-web/pages` until the replacement app has parity.
- Do not split the backend API.
- Do not split PostgreSQL.
- Do not add dependencies unless the target package actually needs them.
- Prefer static generated Nuxt app output for new apps.
- Keep public domains in environment variables and Cloudflare config, not source code.

## Validation Commands

Run from the repository root:

```powershell
npm.cmd run modular:check
npm.cmd run legacy:web:build
npm.cmd run legacy:api:build
```

For deeper backend verification:

```powershell
npm.cmd run legacy:api:test
```

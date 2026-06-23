# Stage 12A.2 Frontend Workspace Foundation

## Purpose

This stage creates a safe, additive foundation for decoupling Garmetix into smaller frontend apps and shared frontend packages.

No existing route is moved or deleted in this stage. The current working Nuxt app remains in `legacy/frontend/garmetix-web` and continues to be the production-safe main/back-office app until each modular app reaches parity.

## Current Safe Layout

| Path | Role |
| --- | --- |
| `legacy/frontend/garmetix-web` | Current working Nuxt app. Treat this as `main-web` for now. |
| `legacy/backend/Garmetix.Api` | Current ASP.NET Core API. Not split in Stage 12. |
| `legacy/backend/Garmetix.Infrastructure` | Current EF Core/PostgreSQL infrastructure. Not split in Stage 12. |
| `modular/apps/*` | Stage 12A generated Nuxt static app shells. These are build-active inside `modular/package.json`. |
| `apps/*` | Stage 12A.2 top-level future app contracts and documentation placeholders. |
| `packages/*` | Stage 12A.2 top-level shared package contracts with minimal `index.ts` exports. |

## Future App Folders

| Folder | Intended app | Current status |
| --- | --- | --- |
| `apps/main-web` | Main/back-office app | Documented as the existing legacy Nuxt app for now. Do not move yet. |
| `apps/pos-web` | POS app | Future focused POS/store operator app. |
| `apps/hr-web` | HR app | Future HR, attendance, payroll app. |
| `apps/ai-sense-web` | AI Sense analytics app | Future analytics/read-only insight app. |
| `apps/saas` | Admin/SaaS app | Future owner/developer/admin control app. |

## Shared Package Folders

| Folder | Purpose |
| --- | --- |
| `packages/api-client` | Shared API URL, request, error, and typed endpoint helpers. |
| `packages/auth-client` | Shared token/session/user/role helpers. |
| `packages/shared-types` | Shared DTO, route, menu, user, and app metadata types. |
| `packages/shared-utils` | Shared date, money, string, validation, and formatting helpers. |
| `packages/shared-ui` | Shared UI contracts and later reusable Vue/Nuxt UI components. |

## Package Manager Decision

The repo uses npm:

- `legacy/frontend/garmetix-web/package-lock.json`
- `modular/package-lock.json`

No pnpm, yarn, or bun workspace is introduced. Root `package.json` is not converted into an npm workspace in this stage. That avoids changing install resolution for the existing legacy app and backend validation scripts.

The existing `modular/package.json` already has an npm workspace for the Stage 12A shell apps. The new top-level `apps/` and `packages/` folders are contracts/placeholders until Stage 12B starts extracting real code.

## How These Folders Will Be Used

1. Stage 12B will extract auth/session primitives from `legacy/frontend/garmetix-web/composables/useAuth.ts` into `packages/auth-client`.
2. Stage 12B will extract request primitives from `legacy/frontend/garmetix-web/composables/useGarmetixApi.ts` into `packages/api-client`.
3. Route/menu/permission types from `AppShell.vue` and `useAccessControl.ts` will be normalized into `packages/shared-types`.
4. Date/money formatting and safe message formatting will move into `packages/shared-utils`.
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


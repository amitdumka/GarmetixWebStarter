# Stage 12F.1 - Admin SaaS Foundation

Version: 5.12.24

## Scope

This stage turns `modular/apps/admin` from a route-list shell into a runnable Admin/SaaS frontend foundation.

## Added

- Admin app shell with sidebar navigation.
- Admin login page.
- Client-side guard for SuperAdmin, Owner and Admin sessions.
- Access denied page.
- Reusable admin table and placeholder components.
- Shared admin API helper.
- Read-only foundation pages:
  - `/`
  - `/setup`
  - `/client-onboarding`
  - `/access`
  - `/license-activation`
  - `/message-logs`
  - `/import-export`
  - `/data-consistency`
  - `/system-health`
  - `/runtime-diagnostics`
  - `/production-readiness`

## Connected Endpoints

- `GET /api/companies`
- `GET /api/store-groups`
- `GET /api/stores`
- `GET /api/access/users`
- `GET /api/access/matrix`
- `GET /api/message-logs`
- `GET /api/runtime-diagnostics`
- `GET /api/runtime-diagnostics/page-contracts`
- `GET /api/production-readiness/summary`
- `GET /api/license/status`
- `GET /api/import-export/modules`
- `GET /api/import-export/center`
- `GET /api/import-export/health`
- `GET /api/client-onboarding/summary`
- `GET /api/client-onboarding/options`

## Safety

- No database schema changes.
- No factory reset, repair, import commit, user create/edit/delete or license activation actions are exposed in this modular foundation.
- SuperAdmin visibility is still enforced by the API for `/api/access/users`; the client guard only prevents accidental access to the Admin app shell.

## Validation

Run:

```powershell
npm.cmd run modular:check
npm.cmd --prefix modular run build:admin
npm.cmd run legacy:api:build
```

## Next Step

Stage 12F.2 should connect Admin safe actions carefully, starting with non-destructive diagnostics and backup/status views before any owner-only write workflow.

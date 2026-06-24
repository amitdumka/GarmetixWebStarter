# Stage 12E.8 - Books Audit Controls

Version: 5.12.22

## Scope

This stage connects the Books frontend to read-only accounting control surfaces:

- Financial year lock register.
- Journal balance validation.
- Accounting-scoped audit trail.
- Accounting-scoped message logs.

The goal is accountant/CA visibility without exposing the full admin audit and message-log surface.

## Backend Additions

The existing ASP.NET Core API remains unified. New read-only endpoints were added under the existing Accounting policy group:

- `GET /api/accounting/audit/recent`
- `GET /api/accounting/audit/events/{auditLogId}`
- `GET /api/accounting/message-logs`

These endpoints filter audit/log data to accounting-relevant modules, entities, routes and sources. Full global audit and message-log views remain in the Admin/SaaS app.

## Frontend Additions

Books now has connected pages:

- `modular/apps/books/pages/financial-year-locks.vue`
- `modular/apps/books/pages/audit.vue`
- `modular/apps/books/pages/message-logs.vue`

The Books sidebar now includes FY Locks, Audit and Message Logs.

## Safety

- No database schema changes.
- No lock, unlock, edit, delete or repair actions were enabled.
- Message logs and audit details are display-only.
- Scope filters respect company/store claims where present.

## Validation

Run:

```powershell
npm.cmd run modular:check
npm.cmd --prefix modular run build:books
npm.cmd run legacy:api:build
```

## Next Step

Stage 12E.9 should add the Books static deployment script and deployment notes for `books.garmetix.aadwikafashion.in`, then Stage 12E can move toward writable accounting slices only after endpoint contracts are approved.

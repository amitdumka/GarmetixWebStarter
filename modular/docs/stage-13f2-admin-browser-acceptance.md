# Stage 13F.2 Admin/SaaS Browser Acceptance

Version: 5.13.37
Branch: Version5

## Purpose

Stage 13F.2 adds a repeatable browser acceptance gate for the modular Admin/SaaS app. The check is dry by default and can run live with Playwright when the Admin static app is serving.

## Covered Screens

- Login as an anonymous route.
- Access denied using a non-admin session.
- Admin home, setup/company, users and roles.
- System health, runtime diagnostics and message logs.
- Backup maintenance and Google Drive backup.
- Import/export, data consistency and license status.
- Client onboarding, production readiness, production support and production rehearsal.

## Guardrails

- Uses a 1366x768 viewport to match a 14 inch laptop at normal zoom.
- Verifies page headings render without document-level horizontal overflow.
- Verifies visible messages do not expose raw localhost, server or API URLs.
- Verifies destructive factory reset, restore, delete, license activation and import commit action labels are not exposed in the modular Admin UI.
- Does not execute any backend write, restore, delete, import, license or reset operation.

## Commands

```bash
npm run modular:admin:browser-acceptance
```

Optional live check when the Admin app is running:

```bash
npm run modular:admin:browser-acceptance -- --live
```

## Next Step

Stage 13F.3 should add guarded writable/live preflight coverage for Admin-only dangerous operations, still keeping actual reset/restore/import/license mutations behind explicit opt-in flags and backend permission review.

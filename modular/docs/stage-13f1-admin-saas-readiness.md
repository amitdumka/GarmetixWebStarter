# Stage 13F.1 Admin/SaaS Readiness

Version: 5.13.36
Branch: Version5

## Purpose

Stage 13F.1 starts Admin/SaaS live-operations hardening with a non-mutating readiness gate. The goal is to verify the Admin app can review setup, access, diagnostics, logs, backup, license, import/export and deployment governance without exposing destructive actions in the modular UI.

## Covered Areas

- Admin/SaaS route ownership and access guard.
- SuperAdmin, Owner and Admin session recognition in the modular middleware.
- Read-only setup/company/store visibility.
- Users, roles and permission matrix review.
- Message log visibility for errors, warnings, events and client logs.
- Runtime diagnostics, system health and migration status.
- Backup, Google Drive backup and restore readiness visibility.
- License status visibility without activation/generation actions.
- Import/export module, center and health visibility without upload/commit actions.
- Data consistency and production readiness visibility.
- Factory reset guardrail review without executing reset.

## Validation Commands

Run the Admin/SaaS readiness gate:

```bash
npm run modular:admin:saas-readiness
```

Run the full dry modular gate:

```bash
npm run modular:validate -- --skip-builds
```

Optional live check when the API is running and an Admin/SaaS token is available:

```bash
npm run modular:admin:saas-readiness -- --live
```

Use `--require-token` and `--strict-permissions` only when validating with a real Owner, Admin or SuperAdmin token.

## Mutation Rule

The readiness gate does not call backup create, restore preview, restore, delete, factory reset, license generate, license activate, import commit or maintenance cleanup endpoints.

## Current Risk

The backend factory reset endpoint is Admin-policy protected and requires exact `FACTORY RESET` confirmation plus a safety backup, but it is not yet restricted to SuperAdmin-only in code. The modular Admin app does not expose factory reset actions in this stage.

## Next Step

Stage 13F.2 should add Admin browser acceptance checks for login, access denied, setup, users, system health, message logs, backup, import/export and production pages on 14 inch laptop layouts.

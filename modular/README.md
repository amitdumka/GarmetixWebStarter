# Garmetix Modular Frontends

This workspace starts the Version5 split without changing the backend API or PostgreSQL database.

## Design Rules

- Keep the ASP.NET Core API as one service.
- Keep PostgreSQL as one database.
- Build smaller static/SPA/PWA-style Nuxt frontends.
- Share auth, API client, types, app registry, and basic UI contracts.
- Keep production domains in environment or deployment config, not source code.

## Apps

- `apps/main`: main back office shell.
- `apps/pos`: billing/POS shell.
- `apps/hr`: employee, attendance, payroll shell.
- `apps/ai-sense`: analytics shell.
- `apps/books`: accounting shell.
- `apps/admin`: Admin/SaaS owner shell.

## Packages

- `packages/shared-api`: common API client boundary.
- `packages/shared-auth`: auth token and permission helpers.
- `packages/shared-types`: common TypeScript contracts.
- `packages/shared-utils`: date, money, string, and safe-message utilities.
- `packages/shared-ui`: shared app registry and layout metadata.

## First Stage

This stage creates the folder base and safe project shells. The actual page migration should happen module by module from `legacy/frontend/garmetix-web` into these apps.

Version5 Stage 12 onward frontend changes should stay inside this `modular/` folder. The `legacy/` folder is the Version4/Stage 11 reference and fallback until modular apps reach parity.

## Deploy

POS static deploy automation is documented in `deploy/README.md`.
Run it from the repository root with environment variables for target host, remote directory, and public API/app URLs.

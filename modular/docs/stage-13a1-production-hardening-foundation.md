# Stage 13A.1 - Production Hardening Foundation

Version: 5.13.1

## Goal

Start Stage 13 with production hardening work that prepares the modular apps for live smoke testing and deployment without changing the shared backend or PostgreSQL database.

## Scope

- Keep one ASP.NET Core API.
- Keep one PostgreSQL database.
- Keep `legacy/` as fallback.
- Keep all Stage 13 work under `modular/` unless a future stage explicitly needs backend hardening.
- Avoid committed credentials, Cloudflare tokens, SSH keys and production database strings.

## Added Command

From the repository root:

```powershell
npm.cmd run modular:smoke:checklist
```

Useful variants:

```powershell
npm.cmd run modular:smoke:checklist -- --mode=public
npm.cmd run modular:smoke:checklist -- --app=pos
npm.cmd run modular:smoke:checklist -- --mode=public --app=hr --write
```

Generated files are written under `modular/docs/generated/` and ignored by git.

## Stage 13 Roadmap

| Stage | Purpose |
| --- | --- |
| 13A.1 | Smoke checklist foundation and production hardening roadmap. |
| 13A.2 | Playwright route smoke tests for local modular apps. |
| 13A.3 | API health and auth state smoke checks. |
| 13A.4 | Public URL smoke mode for Cloudflare Tunnel deployment. |
| 13A.5 | Backend warning cleanup and low-risk hardening items. |
| 13B | POS parity and writable workflow hardening. |
| 13C | HR attendance/payroll parity and device integration hardening. |
| 13D | Books writable accounting workflow verification. |
| 13E | Admin/SaaS production controls and safer operation gates. |

## Immediate Smoke Targets

- Main: dashboard, billing, purchase, inventory, customers and reports.
- POS: sale, held bills, returns, print, day open and day close.
- HR: employees, today attendance, monthly attendance, payroll review, salary payment and devices.
- AI Sense: dashboard, sales, purchase, profit and stock-risk analytics.
- Books: accounting, vouchers, petty cash, vendor payments, GST, audit and message logs.
- Admin/SaaS: setup, access, system health, runtime diagnostics, production readiness and message logs.

## Acceptance

- `npm.cmd run modular:smoke:checklist` prints a local smoke checklist.
- `npm.cmd run modular:smoke:checklist -- --mode=public --app=pos --write` creates an ignored local checklist.
- `npm.cmd run modular:check` passes.
- No secrets are committed.

## Next Step

Stage 13A.2 should add Playwright-based local route smoke tests using this checklist as the route source.

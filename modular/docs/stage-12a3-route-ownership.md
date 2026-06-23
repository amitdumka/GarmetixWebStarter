# Stage 12A.3 Route Ownership Registry

## Purpose

Stage 12A.3 creates a single route ownership registry for the modular frontend split.

The source file is:

```text
modular/config/routes.ts
```

This file does not move or delete legacy pages. It tells later stages which modular app should own each route.

## App Ownership

| App | Folder | Responsibility |
| --- | --- | --- |
| Main | `modular/apps/main` | Back office, dashboards, purchase, inventory, reports, store operations, customers. |
| POS | `modular/apps/pos` | Sale screen, barcode billing, returns, off-book counter flows, day open/day close. |
| HR | `modular/apps/hr` | Employees, attendance, monthly attendance, payroll, salary payments, kiosk devices. |
| Books | `modular/apps/books` | Accounting, ledgers, vouchers, petty cash, banking, GST, audit-facing finance. |
| AI Sense | `modular/apps/ai-sense` | Sales, purchase, profit, stock risk, vendor, customer, daily and monthly analytics. |
| Admin | `modular/apps/admin` | Company setup, users, roles, permissions, license, health, logs, deployment readiness. |

## Registry Fields

Each route entry includes:

- `id`
- `path`
- `label`
- `icon`
- `targetApp`
- `moduleKey`
- `moduleLabel`
- `roles`
- `externalUrlEnvKey`
- `externalUrlEnvAliases`
- `legacyPath`
- `showInMenu`
- `status`
- `notes`

## Link Behavior

For modular app links:

- `main` routes can remain internal while the main modular app is being built.
- non-main routes use the target app URL when configured.
- if a target app URL is missing, the registry returns the legacy route path as a safe fallback.
- no route should break just because an environment URL is missing.

Preferred env keys:

- `NUXT_PUBLIC_GARMETIX_MAIN_URL`
- `NUXT_PUBLIC_GARMETIX_POS_URL`
- `NUXT_PUBLIC_GARMETIX_HR_URL`
- `NUXT_PUBLIC_GARMETIX_AI_SENSE_URL`
- `NUXT_PUBLIC_GARMETIX_BOOKS_URL`
- `NUXT_PUBLIC_GARMETIX_ADMIN_URL`
- `NUXT_PUBLIC_GARMETIX_API_BASE_URL`

Prompt compatibility aliases are also listed in `modular/.env.example`.

## Immediate Next Step

Stage 12A.4 should wire `garmetixRoutes` and `buildAppTargetLinks()` into shared shell/menu helpers so every modular app can render only its own routes and optional app-switch links.


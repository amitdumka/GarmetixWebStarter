# Stage 12 Frontend Decoupling Plan

## Current Structure Summary

This audit uses the Version5 Stage 12A layout.

| Area | Current path | Notes |
| --- | --- | --- |
| Legacy frontend root | `legacy/frontend/garmetix-web` | Existing Nuxt app. This is the source for route extraction. |
| New modular frontend root | `modular` | Stage 12A shell workspace with six Nuxt app shells and shared package folders. |
| Backend/API root | `legacy/backend/Garmetix.Api` | ASP.NET Core Web API. Keep as one API for Stage 12. |
| Domain project | `legacy/backend/Garmetix.Domain` | Shared domain models and generated models. |
| Infrastructure project | `legacy/backend/Garmetix.Infrastructure` | EF Core DbContext, migrations, repository wiring. |
| Tests | `legacy/backend/Garmetix.Api.Tests` | Existing backend test project. |
| Package manager | npm | `package-lock.json` exists in legacy frontend and modular workspace. No pnpm/yarn/bun lockfile was found. |
| Legacy Nuxt version | Nuxt `^4.4.8` | From `legacy/frontend/garmetix-web/package.json`. |
| Legacy Nuxt UI version | Nuxt UI `^4.9.0` | Stage 12A modular workspace pins `@nuxt/ui` exactly to `4.9.0`. |
| Legacy app mode | Nuxt server build | `npm run build` runs `nuxt build`; Docker runs Nitro node server on port 3000. |
| Modular app target | Static SPA-style Nuxt | Stage 12A app shells use `ssr: false` and `nuxt generate`. |

## Current Nuxt Config

Legacy config: `legacy/frontend/garmetix-web/nuxt.config.ts`

- Uses `@nuxt/ui`.
- Uses dark mode by default through `colorMode.preference = 'dark'`.
- Public API base is `NUXT_PUBLIC_API_BASE`, default `/api`.
- Internal API proxy base is `NUXT_API_INTERNAL_BASE`, default `http://localhost:5080/api`.
- Uses a local `/api` proxy route in `server/api/[...path].ts` to forward browser calls through Nuxt to the ASP.NET API.
- Uses app icons and manifest from `public/`.
- Font providers are disabled/local in the legacy config, which avoids external font lookups during build.

## Existing Frontend Route List

Routes are generated from `legacy/frontend/garmetix-web/pages`.

```text
/
/:module
/about-us
/access
/access-denied
/accounting
/af-ss
/attendance
/attendance/biometric-enrollment
/attendance/device-bridge
/attendance/devices
/attendance/face-liveness
/attendance/final-acceptance
/attendance/kiosk
/attendance/kiosk-monitor
/attendance/mobile-kiosk
/attendance/mobile-kiosk-rehearsal
/attendance/monthly
/attendance/payroll-review
/attendance/payroll-summary
/attendance/photo-review
/attendance/policies
/attendance/regularization
/attendance/salary-draft
/attendance/salary-payment
/attendance/shifts
/attendance/today
/audit
/audit-trail-final
/backup-maintenance
/barcode-final-acceptance
/billing
/billing/new
/cash-details
/cash-vouchers
/client-onboarding
/commercial-notes
/contact-us
/credit-notes
/credit-notes/:id
/credit-notes/new
/customers
/customers/:id
/customers/new
/dashboard
/dashboard/business
/dashboard/map
/dashboard/store-manager
/dashboard/todays
/data-consistency
/debit-notes
/debit-notes/:id
/debit-notes/new
/document-scan
/email-delivery
/faq
/financial-year-locks
/google-drive-backup
/gst-final-acceptance
/gst-production
/gst-reports
/gst-returns
/hr
/hr-benefits
/import-export
/inventory
/license-activation
/loyalty
/message-logs
/non-gst-goods
/oracle-sync
/parties
/payroll
/permission-final-acceptance
/petty-cash
/post-go-live-acceptance
/print-final-acceptance
/production-final-acceptance
/production-readiness
/production-rehearsal
/production-support
/profile
/purchase
/purchase-return
/purchase/new
/release-stabilization
/reports
/runtime-diagnostics
/sales-return
/setup
/stage10-final-acceptance
/stage10k-operator-acceptance
/stage8g-completion
/stock-operations
/stock-reports
/store-day
/system-health
/system-info
/tailoring
/ui-audit
/vendor-payments
/vendor-settlements
/vouchers
```

## Current Sidebar/Menu Route List

Menu source: `legacy/frontend/garmetix-web/components/AppShell.vue`. `AppShellLegacy.vue` mirrors much of the same navigation for legacy shell mode.

| Group | Routes |
| --- | --- |
| Dashboards | `/dashboard`, `/dashboard/todays`, `/dashboard/store-manager`, `/store-day`, `/dashboard/business`, `/dashboard/map`, `/` |
| Sales | `/billing`, `/billing/new`, `/sales-return`, `/tailoring` |
| Purchase | `/purchase`, `/purchase/new`, `/vendor-payments`, `/purchase-return`, `/vendor-settlements` |
| Inventory | `/inventory`, `/stock-operations`, `/stock-reports` |
| Accounting | `/accounting`, `/financial-year-locks`, `/petty-cash`, `/cash-details`, `/vouchers`, `/debit-notes`, `/debit-notes/new`, `/credit-notes`, `/credit-notes/new`, `/commercial-notes` |
| CRM | `/customers`, `/customers/new`, `/parties`, `/loyalty` |
| GST | `/gst-returns`, `/gst-reports`, `/gst-final-acceptance`, `/gst-production` |
| Reports | `/reports`, `/document-scan`, `/print-final-acceptance`, `/barcode-final-acceptance` |
| Off Book | `/non-gst-goods`, `/cash-vouchers` |
| People | `/hr`, `/attendance`, `/attendance/kiosk`, `/attendance/mobile-kiosk`, `/attendance/mobile-kiosk-rehearsal`, `/attendance/today`, `/attendance/monthly`, `/attendance/shifts`, `/attendance/policies`, `/attendance/devices`, `/attendance/kiosk-monitor`, `/attendance/photo-review`, `/attendance/biometric-enrollment`, `/attendance/face-liveness`, `/attendance/regularization`, `/attendance/payroll-summary`, `/attendance/payroll-review`, `/attendance/salary-draft`, `/attendance/salary-payment`, `/attendance/device-bridge`, `/attendance/final-acceptance`, `/hr-benefits`, `/payroll` |
| Admin | `/setup`, `/client-onboarding`, `/af-ss`, `/access`, `/permission-final-acceptance` |
| Data | `/import-export`, `/data-consistency`, `/message-logs`, `/audit`, `/audit-trail-final`, `/ui-audit` |
| Maintenance | `/system-health`, `/runtime-diagnostics`, `/backup-maintenance`, `/google-drive-backup`, `/production-readiness`, `/production-final-acceptance`, `/stage10-final-acceptance`, `/stage10k-operator-acceptance`, `/production-support`, `/production-rehearsal`, `/email-delivery`, `/license-activation`, `/stage8g-completion`, `/post-go-live-acceptance`, `/release-stabilization`, `/oracle-sync` |
| System | `/system-info` |
| Account | `/profile` |
| Help | `/about-us`, `/contact-us`, `/faq` |

## Existing Auth And Token Handling

Source: `legacy/frontend/garmetix-web/composables/useAuth.ts`

- Auth calls use `/api/auth/login`, `/api/auth/bootstrap-status`, `/api/auth/bootstrap-admin`, `/api/auth/me`, `/api/auth/change-password`, and password reset endpoints.
- Session is stored in localStorage:
  - `garmetix.token`
  - `garmetix.user`
  - `garmetix.expiresAtUtc`
- `useAuth().restore()` hydrates client state from localStorage.
- `auth.global.ts` protects routes client-side and redirects unauthenticated users to `/`.
- Unauthorized API responses call `useAuth().handleUnauthorized(true)`.
- Role helpers include `isOwner`, `isAdmin`, `canSeeAdmin`, `canEdit`, and `canDelete`.

Important migration note: localStorage is origin-scoped. `pos.garmetix...`, `hr.garmetix...`, `books.garmetix...`, and `admin.garmetix...` will not automatically share `garmetix.token`. The first modular version can require login per app, but true SSO needs a backend-supported secure cookie, token exchange, or central login handoff.

## Existing API Client And Composables

Primary API client: `legacy/frontend/garmetix-web/composables/useGarmetixApi.ts`

- Reads `config.public.apiBase`.
- Adds `Authorization: Bearer <token>` from `useAuth` or localStorage.
- Provides `list`, `get`, `create`, `update`, `remove`.
- Removes empty `id` on create to avoid Guid JSON errors.
- Adds request metadata to thrown errors.
- Caches selected lookup resources:
  - companies
  - stores
  - store-groups
  - workspace/options
  - product-categories
  - product-sub-categories
  - brands
  - taxes
  - ledgers
  - parties
  - employees
  - bank-accounts

Other important composables:

- `useWorkspace.ts`: selected company/store group/store state and localStorage persistence.
- `useAccessControl.ts`: route permission rules and menu filtering.
- `useUiFeedback.ts`: toast/message-log bridge and safe error text cleanup.
- `useProductLookup.ts`: product lookup cache.
- `useServerDocumentPrint.ts`: server PDF print/download helper.
- `useAttendance*.ts`: attendance-specific API wrappers.
- `useDashboardPreferences.ts`: dashboard preference/favorite route state.

## Existing Permission And Role Checks

Source: `legacy/frontend/garmetix-web/composables/useAccessControl.ts`

Roles currently normalized into app access roles:

- `admin`
- `owner`
- `accountant`
- `remoteAccountant`
- `powerUser`
- `storeManager`
- `salesman`
- `hr`
- `payroll`
- `member`
- `authenticated`

Access rules are route-based. Unknown routes are currently allowed by default, so modular apps should use an explicit route registry and deny unknown protected routes unless intentionally public.

Current route-rule modules include Dashboards, Reports, GST, Sales, Inventory, Off Book, Purchase, CRM, Accounting, Store Operations, People, Admin, Data, System, Maintenance, Account, and Help.

## Existing Docker, Compose, Proxy And Cloudflare Setup

Primary files:

- `legacy/docker-compose.yml`
- `legacy/docker-compose.prod.yml`
- `legacy/frontend/garmetix-web/Dockerfile`
- `legacy/backend/Garmetix.Api/Dockerfile`
- `legacy/deploy/docker-compose.cloudflare.yml`
- `legacy/infra/cloudflare/config.example.yml`
- `legacy/deploy/cloudflare-create-or-update-tunnel.sh`
- `legacy/deploy/run-production.sh`

Current behavior:

- Development compose runs `api`, `web`, and `postgres`.
- Production compose binds API, web, and PostgreSQL to `127.0.0.1` host ports.
- Cloudflare Tunnel currently publishes the single public web hostname to the Nuxt web service.
- The legacy Nuxt server proxies browser `/api/*` calls to the internal API via `NUXT_API_INTERNAL_BASE`.
- Production CORS currently expects a single frontend origin through `CORS_ALLOWED_ORIGINS`.

Stage 12 migration impact:

- Static modular apps cannot rely on the legacy Nuxt `/api` server proxy unless each app is deployed behind its own proxy.
- Recommended modular target is direct API access through `api.garmetix.aadwikafashion.in`, configured by env.
- Backend CORS must include every frontend origin:
  - `https://garmetix.aadwikafashion.in`
  - `https://pos.garmetix.aadwikafashion.in`
  - `https://hr.garmetix.aadwikafashion.in`
  - `https://ai-sense.garmetix.aadwikafashion.in`
  - `https://books.garmetix.aadwikafashion.in`
  - `https://admin.garmetix.aadwikafashion.in`
- Cloudflare Tunnel ingress must grow from one hostname to six frontend hostnames plus API hostname.

## Suggested Route Ownership

### Main Back Office App

Keep broad operational workflows here first. Some routes may later move fully into POS, HR, or Books, but initially the main app can retain links for owners/admins.

- `/dashboard`
- `/dashboard/todays`
- `/dashboard/store-manager`
- `/dashboard/business`
- `/dashboard/map`
- `/store-day`
- `/purchase`
- `/purchase/new`
- `/purchase-return`
- `/vendor-payments`
- `/vendor-settlements`
- `/inventory`
- `/stock-operations`
- `/stock-reports`
- `/customers`
- `/customers/new`
- `/customers/:id`
- `/parties`
- `/loyalty`
- `/reports`
- `/document-scan`
- `/tailoring`
- `/profile`
- `/about-us`
- `/contact-us`
- `/faq`

### POS App

POS should be fast and focused for store operators, cashier/salesman roles, and day operations.

- `/billing`
- `/billing/new`
- `/sales-return`
- `/customers`
- `/customers/new`
- `/customers/:id`
- `/loyalty`
- `/store-day`
- `/dashboard/store-manager`
- `/cash-vouchers`
- `/non-gst-goods`
- `/document-scan`
- `/tailoring`

### HR App

HR app should own attendance, employee, benefit, payroll preparation, and salary posting screens.

- `/hr`
- `/attendance`
- `/attendance/today`
- `/attendance/monthly`
- `/attendance/shifts`
- `/attendance/policies`
- `/attendance/devices`
- `/attendance/kiosk`
- `/attendance/kiosk-monitor`
- `/attendance/mobile-kiosk`
- `/attendance/mobile-kiosk-rehearsal`
- `/attendance/photo-review`
- `/attendance/regularization`
- `/attendance/biometric-enrollment`
- `/attendance/face-liveness`
- `/attendance/device-bridge`
- `/attendance/payroll-summary`
- `/attendance/payroll-review`
- `/attendance/salary-draft`
- `/attendance/salary-payment`
- `/attendance/final-acceptance`
- `/hr-benefits`
- `/payroll`

### AI Sense App

AI Sense should start read-only and analytics-first. It can consume existing dashboard/report endpoints before any new AI backend is added.

- `/dashboard/todays`
- `/dashboard/business`
- `/reports`
- `/stock-reports`
- `/gst-reports`
- `/document-scan`
- Future: trend alerts, anomaly detection, sales/inventory insights, payroll risk insights.

### Books / Accounting App

Books should focus on accountant/CA workflows and financial audit scrutiny.

- `/accounting`
- `/financial-year-locks`
- `/petty-cash`
- `/cash-details`
- `/vouchers`
- `/debit-notes`
- `/debit-notes/new`
- `/debit-notes/:id`
- `/credit-notes`
- `/credit-notes/new`
- `/credit-notes/:id`
- `/commercial-notes`
- `/gst-returns`
- `/gst-reports`
- `/gst-production`
- `/vendor-payments`
- `/vendor-settlements`
- `/audit`
- `/audit-trail-final`
- `/message-logs`

### Admin / SaaS Frontend

Admin/SaaS should be owner/developer/super-admin focused. It should not become the daily store operator app.

- `/setup`
- `/client-onboarding`
- `/af-ss`
- `/access`
- `/permission-final-acceptance`
- `/import-export`
- `/data-consistency`
- `/message-logs`
- `/audit`
- `/ui-audit`
- `/system-health`
- `/system-info`
- `/runtime-diagnostics`
- `/backup-maintenance`
- `/google-drive-backup`
- `/production-readiness`
- `/production-final-acceptance`
- `/stage10-final-acceptance`
- `/stage10k-operator-acceptance`
- `/production-support`
- `/production-rehearsal`
- `/email-delivery`
- `/license-activation`
- `/stage8g-completion`
- `/post-go-live-acceptance`
- `/release-stabilization`
- `/oracle-sync`
- `/gst-final-acceptance`
- `/print-final-acceptance`
- `/barcode-final-acceptance`

## Shared Code Candidates

| Candidate | Source today | Target |
| --- | --- | --- |
| API client | `legacy/frontend/garmetix-web/composables/useGarmetixApi.ts` | `modular/packages/shared-api` |
| Auth helper | `legacy/frontend/garmetix-web/composables/useAuth.ts` | `modular/packages/shared-auth` |
| Permission helper | `legacy/frontend/garmetix-web/composables/useAccessControl.ts` | `modular/packages/shared-auth` or `modular/packages/shared-types` plus app adapters |
| Workspace helper | `legacy/frontend/garmetix-web/composables/useWorkspace.ts` | `modular/packages/shared-auth` or `modular/packages/shared-api` |
| Common types/interfaces | Inline composable/page types and generated DTO shapes | `modular/packages/shared-types` |
| Date/money formatting | Currently scattered in pages/components | `modular/packages/shared-utils` |
| Error/message handling | `useUiFeedback.ts`, `utils/applicationMessageLog.ts`, `plugins/application-message-log.client.ts` | Shared UI/API package split |
| Product lookup | `useProductLookup.ts` | Shared API package, used by POS and main app |
| Document print | `useServerDocumentPrint.ts` | Shared API/UI package, used by POS, Books, HR |
| Basic UI components | `components/ui/*`, dashboard cards/tables, entry forms where generic | `modular/packages/shared-ui` |
| App registry/menu metadata | `AppShell.vue` module groups and `useAccessControl.ts` rules | `modular/packages/shared-types` or `modular/config/routes.ts` |

## Recommended Target Folder Structure

```text
GarmetixWebStarter/
  legacy/
    backend/
    frontend/garmetix-web/
    deploy/
    infra/
  modular/
    apps/
      main/
      pos/
      hr/
      ai-sense/
      books/
      admin/
    packages/
      shared-api/
      shared-auth/
      shared-types/
      shared-utils/
      shared-ui/
    config/
      apps.ts
      routes.ts
      permissions.ts
    docs/
    deploy/
      docker/
      cloudflare/
```

Recommended route registry shape:

```text
route id -> path -> label -> module -> owner app -> fallback app -> roles -> menu group
```

This gives one source for menus, permissions, and route extraction instead of copying `AppShell.vue` arrays into each app.

## Risks And Migration Notes

- Auth sharing across subdomains is not automatic because current tokens are in localStorage. Plan for login-per-app first or implement backend-supported SSO later.
- Static modular apps should use an API hostname from env, not the legacy Nuxt `/api` proxy.
- CORS must be updated before real modular apps are exposed outside localhost.
- Menu and permission logic are duplicated conceptually between `AppShell.vue` and `useAccessControl.ts`; extraction should consolidate them.
- Some routes belong to more than one app. For example customers are needed by POS and main back office; reports are needed by main, books, and AI Sense.
- Keep the legacy app working until each module has parity in its modular target.
- Move pages module by module, not by large copy-paste. Each extraction should include build validation.
- Existing Dockerfiles still assume the legacy folder structure when run from inside `legacy/`. Version5 deployment should get new compose files only after shared auth/API extraction is stable.
- API route permission remains backend-enforced where available; frontend permission checks are only UX/access filtering and must not be treated as security alone.

## Exact Next Implementation Steps

1. Keep the shared route registry in `modular/config/routes.ts` as the source of truth for modular ownership and app links.
2. Wire route/menu helpers into shared shell UI so each modular app shows only its owned routes.
3. Extract `useAuth` session types and token helpers into `modular/packages/shared-auth` without changing legacy behavior.
4. Extract `useGarmetixApi` request logic into `modular/packages/shared-api`, keeping auth headers injectable so every app can reuse it.
5. Extract route-role rules from `useAccessControl.ts` into shared permission utilities.
6. Add a minimal login/auth guard to all six modular app shells using the shared auth package.
7. Add one smoke page per modular app that calls `/health` or `/auth/bootstrap-status` through the shared API client.
8. Validate all six static modular builds.
9. Only after shared auth/API is stable, begin route extraction in this order:
   - POS: `/billing/new`, product lookup, customer lookup.
   - HR: attendance dashboard and today/monthly attendance.
   - Books: accounting, vouchers, petty cash.
   - Admin: system health, message logs, users/roles.
   - AI Sense: dashboards and reports.
10. Update Docker/Cloudflare deployment after at least one modular app has real route parity.
11. Keep `legacy/` available as fallback until all target apps pass acceptance.

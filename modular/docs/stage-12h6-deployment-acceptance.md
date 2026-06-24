# Stage 12H.6 - Deployment Acceptance

Version: 5.12.36

## Goal

Define when each Version5 modular frontend can be considered deploy-ready while keeping the current architecture intact:

- One ASP.NET Core API.
- One PostgreSQL database.
- One shared auth/token/permission system.
- Multiple static Nuxt frontends.
- `legacy/` remains available until modular parity is accepted.

## Global Acceptance Gates

All modular apps must satisfy these gates before a production deployment:

| Gate | Acceptance rule |
| --- | --- |
| Branch state | `Version5` is synced with `origin/Version5` and the working tree is clean. |
| Version marker | Root package, modular package, app packages, lockfile and `modular/config/version.ts` share the same stage version. |
| Structure | `npm.cmd run modular:check` passes. |
| Builds | `npm.cmd run modular:validate` passes before release. |
| Preflight | `npm.cmd run modular:deploy:preflight` passes locally. |
| Secrets | No passwords, Cloudflare tokens, private keys or database strings are committed. |
| API | `api.garmetix.aadwikafashion.in` reaches the ASP.NET API, not a static frontend. |
| Database | No database split, no new production database, and no destructive migration for frontend-only deployment. |
| Rollback | Previous static release folders are retained and the `current` symlink can be rolled back. |

## Shared Runtime Acceptance

- Each frontend uses `NUXT_PUBLIC_GARMETIX_API_BASE_URL`.
- Login/token storage remains compatible across apps.
- Role and permission behavior is enforced by the API and respected by each frontend shell.
- Unauthorized users see access-denied or hidden navigation, not broken pages.
- API error messages shown in the UI do not expose local server URLs or internal stack traces.
- Message logs remain the system of record for operational errors when backend endpoints write them.

## App Acceptance Matrix

| App | Host | Deploy-ready when |
| --- | --- | --- |
| Main Back Office | `garmetix.aadwikafashion.in` | Dashboard, store-day, billing review, purchase review, inventory, customers, reports and support pages load through the Main shell and can reach API health. |
| POS | `pos.garmetix.aadwikafashion.in` | Sale, held bills, returns, day open, day close and print handoff routes load quickly and remain compatible with existing sale/print APIs. |
| HR | `hr.garmetix.aadwikafashion.in` | Employee, attendance, monthly attendance, payroll review, salary draft/payment and device pages load, with destructive salary/attendance actions still guarded by verified endpoint contracts. |
| AI Sense | `ai-sense.garmetix.aadwikafashion.in` | Read-only analytics pages load with empty/loading/error states and connect to existing `/api/ai-sense/*` endpoints where available. |
| Books | `books.garmetix.aadwikafashion.in` | Accounting, vouchers, ledgers, parties, banking, petty cash, vendor payments, GST, audit and message-log review pages load read-only unless a writable flow has been explicitly verified. |
| Admin/SaaS | `admin.garmetix.aadwikafashion.in` | Setup, access, diagnostics, license, import/export, backup, production support and rehearsal pages load with SuperAdmin/admin visibility rules preserved. |

## App-Specific Smoke Checks

### Main Back Office

- [ ] Login works.
- [ ] Dashboard summary cards load or show a controlled empty state.
- [ ] Billing, purchase, inventory, customers and reports routes are reachable.
- [ ] App switch links route to POS, HR, AI Sense, Books and Admin hosts.
- [ ] Logout clears session and returns to login.

### POS

- [ ] Login works.
- [ ] Sale route can search/select products or show a controlled no-data state.
- [ ] Held bill flow can hold/resume/remove locally.
- [ ] Returns route handles invoice lookup failures cleanly.
- [ ] Day open and day close routes can reach store-day API or show a controlled error.
- [ ] Print route opens without layout breakage.

### HR

- [ ] Login works.
- [ ] Employee and attendance pages load.
- [ ] Today and monthly attendance pages show available data or controlled empty states.
- [ ] Payroll review and salary draft pages do not create final slips unless explicitly triggered by verified endpoints.
- [ ] Salary payment preview handles advance/due data returned by the API without frontend crashes.
- [ ] Device pages explain unavailable fingerprint devices without blocking HR navigation.

### AI Sense

- [ ] Login works.
- [ ] Business dashboard and analysis pages load.
- [ ] Sales, purchase, profit, stock risk, vendor, customer, daily and monthly routes have loading/empty/error states.
- [ ] Analytics pages remain read-only.
- [ ] API failures do not expose internal URLs or stack traces.

### Books

- [ ] Login works.
- [ ] Ledger groups, ledgers, parties and bank review pages load.
- [ ] Voucher, petty cash, vendor payment and GST review pages remain read-only unless explicitly accepted for write actions.
- [ ] Audit and message log pages load.
- [ ] PDF/export handoff buttons are visible only when source records expose valid IDs.
- [ ] Financial-year lock visibility is preserved.

### Admin/SaaS

- [ ] Login works.
- [ ] SuperAdmin/admin-only pages are not visible to normal store users.
- [ ] Setup, access, diagnostics, production readiness, import/export and backup pages load.
- [ ] Dangerous operations remain absent or clearly guarded until endpoint contracts are verified.
- [ ] Runtime diagnostics and production support pages do not expose secrets.

## Release Go/No-Go

Go only if:

- [ ] Full validation passes.
- [ ] Local preflight passes.
- [ ] Release checklist has been generated or reviewed.
- [ ] API health is reachable through the public API host.
- [ ] Database backup is complete.
- [ ] At least one rollback release exists after the first production deploy.
- [ ] Known warnings are documented and accepted.

No-go if:

- [ ] Any modular build fails.
- [ ] API build fails.
- [ ] Secret scan finds credentials or keys.
- [ ] Public API host points to the wrong service.
- [ ] Login fails in any app included in the release.
- [ ] A user can see an app/module outside their role.
- [ ] A writable accounting, HR or admin action is exposed without verified endpoint behavior.

## Known Accepted Warnings

- Windows may warn that Bash/rsync deploy scripts need Git Bash, WSL, or Linux. This is acceptable for local Windows checks.
- Nuxt may emit external font provider certificate warnings on Windows. The deployment decision should follow the command exit code and visual smoke checks.
- Existing backend nullable warnings should be fixed in a backend hardening stage, but they do not block static frontend deployment when the API build exits successfully.

## Next Step

Stage 12Z should consolidate final verification: full modular validation, legacy API build, deploy docs review, no database split confirmation, and final known-issues documentation.

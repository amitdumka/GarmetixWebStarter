# Garmetix Version5 Modular TODO

This is the working TODO for Version5 Stage 12 onward. All frontend decoupling work belongs under `modular/`. The `legacy/` folder remains the Version4/Stage 11 fallback until modular apps reach parity.

Prompts and external plans are reference material, not strict folder orders. Each step should be adapted to the existing `modular/apps/*`, `modular/packages/*`, `modular/config/*`, and `modular/docs/*` structure.

## Process Rules

1. Pull/check the current `Version5` branch before starting a module.
2. Keep changes small and commit after each completed module/stage.
3. Keep the ASP.NET API and PostgreSQL database unified unless a future stage explicitly changes that decision.
4. Keep `legacy/` behavior intact until modular parity is proven.
5. Add or update docs for each stage.
6. Run the safest available validation after each stage.
7. Do not hardcode server passwords or production secrets in source control.
8. When deployment automation is added, use SSH keys, environment variables, or prompted credentials.

## Stage 12A: Registry And Foundation

Status: in progress.

- 12A.1: Audit current project and document route ownership.
- 12A.2: Keep Version5 modular workspace under `modular/`.
- 12A.3: Create `modular/config/routes.ts` and document ownership.
- 12A.4: Build app switch/sidebar link helpers from the route registry.
- 12A.5: Add shared shell layout contracts for all modular apps.
- 12A.5 complete: shared API health, auth snapshot, and shell status cards.

## Stage 12B: POS First

Goal: split the fastest billing counter workflows first.

- 12B.1 complete: add POS route shell, login bridge, and first route pages inside `modular/apps/pos`.
- 12B.2 complete: add POS sale draft with product lookup, cart totals, payments, save and print.
- 12B.3 complete: add POS customer profile adjustments and print queue hardening.
- 12B.4 complete: add POS route guard, scanner focus, keyboard shortcuts, and save validation polish.
- 12B.5 complete: add POS sales return invoice lookup, item selection, credit note save, and print handoff.
- 12B.6 complete: add POS local held-bill queue with hold, resume, remove, and shortcut support.
- 12B.7 complete: connect POS day open/close to store-day API with petty cash preview and print.
- 12B.8 complete: add POS static deploy script and deployment notes.
- Planned routes: `/login`, `/`, `/day-open`, `/sale`, `/hold-bills`, `/returns`, `/print`, `/day-close`.
- Reuse `modular/packages/shared-api` and `modular/packages/shared-auth`.
- Keep sale invoice and print flows compatible with existing API behavior.
- Add POS build validation.
- POS Ubuntu static deploy script is available at `modular/deploy/pos-static-deploy.sh`.

## Stage 12C: HR

Goal: split employees, attendance, payroll, and salary payment screens.

- Add HR pages inside `modular/apps/hr`.
- Planned routes: `/login`, `/`, `/employees`, `/attendance`, `/attendance-summary`, `/payroll`, `/payslips`, `/salary-payments`, `/kiosk-devices`.
- Use existing attendance/payroll endpoints where available.
- Keep salary and attendance pages non-destructive until endpoint contracts are verified.
- Add HR build validation.
- After HR builds, add an Ubuntu deploy script for HR static output.

## Stage 12D: AI Sense

Goal: build read-only analytics first, then connect backend endpoints.

- Add AI Sense pages inside `modular/apps/ai-sense`.
- Planned routes: `/login`, `/`, `/sales-analysis`, `/purchase-analysis`, `/profit-analysis`, `/stock-risk`, `/vendor-analysis`, `/customer-analysis`, `/daily-summary`, `/monthly-summary`.
- Add empty/loading/error states before backend analytics endpoints are complete.
- Add read-only analytics endpoints to the existing ASP.NET API.
- Connect AI Sense pages to real endpoints.
- After AI Sense builds, add an Ubuntu deploy script for AI Sense static output.

## Stage 12E: Books

Goal: split accountant/CA workflows.

- Add Books pages inside `modular/apps/books`.
- Planned areas: accounting dashboard, ledgers, parties, vouchers, petty cash, cash details, debit notes, credit notes, GST reports, GST returns, audit/message logs.
- Keep banking and audit-sensitive flows explicit.
- After Books builds, add an Ubuntu deploy script for Books static output.

## Stage 12F: Admin/SaaS

Goal: split owner/developer/admin controls.

- Add Admin pages inside `modular/apps/admin`.
- Planned areas: setup/company/store, users/roles/permissions, license, feature/module enablement, client onboarding, system health, message logs, backup/restore, deployment diagnostics.
- Keep SuperAdmin-specific visibility rules explicit.
- After Admin builds, add an Ubuntu deploy script for Admin static output.

## Stage 12G: Main Back Office Cleanup

Goal: make `modular/apps/main` a lean back-office app instead of a catch-all UI.

- Keep dashboard, purchase, inventory, reports, customer/vendor operations, and store operations.
- Remove heavy POS/HR/AI/Books work from main only after target apps have parity.
- Optimize route-level loading and avoid layout-level data fetching.

## Stage 12H: Deployment Split

Goal: deploy each app independently while keeping one API and one database.

- Add static build output handling per app.
- Add deployment scripts under `modular/deploy/`.
- Target hosts:
  - Ubuntu server: `amit@192.168.11.126`
  - Ubuntu desktop: `amitkumar@192.168.11.127`
- Do not store passwords in scripts. Use SSH keys or prompt at runtime.
- Add Cloudflare Tunnel examples for:
  - `garmetix.aadwikafashion.in`
  - `pos.garmetix.aadwikafashion.in`
  - `hr.garmetix.aadwikafashion.in`
  - `books.garmetix.aadwikafashion.in`
  - `ai-sense.garmetix.aadwikafashion.in`
  - `admin.garmetix.aadwikafashion.in`
  - `api.garmetix.aadwikafashion.in`

## Stage 12Z: Verification

Goal: final verification before calling Stage 12 complete.

- Build legacy web.
- Build legacy API.
- Build modular main, POS, HR, AI Sense, Books, and Admin.
- Validate env examples.
- Validate Cloudflare/deploy docs.
- Confirm no database split and no destructive migration.
- Document known issues and next stage.


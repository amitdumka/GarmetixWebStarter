# AntiGravity To Codex TODO

Source reviewed: `D:\AIArea\GarmetixWebStarter\AntigravityAIChanges.md`

Purpose: convert the Antigravity Version6.A change log into a Codex Version6 review and porting checklist. Do not merge these items blindly. Each item must be inspected against the current Version6 architecture, backend contracts, live deployment rules, and user-approved module order.

## Immediate Safety Notes

- Keep Codex Version6 as the active implementation branch.
- Read Version6.A files only when explicitly requested or when a single TODO item is being reviewed.
- Do not copy hardcoded production API URLs into modular apps.
- Do not bypass SSL verification in Nuxt configs.
- Do not delete existing working modular pages just because Version6.A deleted stubs.
- Do not split the backend or PostgreSQL database.
- Keep secrets and deployment config outside git.

## Links Mentioned

- No external links were present in `AntigravityAIChanges.md`.
- Referenced source paths are local repo paths under `D:\AIArea\GarmetixWebStarter`.

## Priority 0 - Review Before Any Port

- [ ] Compare Version6.A file list with Codex Version6 for the exact changed files before applying any code.
- [ ] Identify changes that are already implemented in Codex Version6.
- [ ] Identify changes that conflict with Codex Version6 deployment/security rules.
- [ ] Confirm whether Version6.A has backend database/schema changes that require migration review.
- [ ] Confirm whether Version6.A changed route ownership, package workspace names, or app ports.

## Priority 1 - Backend SaaS And Tenant Infrastructure

- [ ] Review `backend/Garmetix.Domain/Generated/Models/SaaS/`.
  - Validate `Tenant` and `Subscription` model shape.
  - Check whether these models belong in generated domain models or hand-written SaaS/domain folder.
  - Confirm migration and seed impact before porting.
- [ ] Review `backend/Garmetix.Infrastructure/Data/GarmetixDbContext.cs`.
  - Inspect global query filter implementation.
  - Ensure `TenantId` isolation does not break existing company/store scoped live data.
  - Verify SuperAdmin access behavior and background jobs.
  - Add tests or smoke checks before enabling globally.
- [ ] Review `backend/Garmetix.Api/Licensing/LicenseEndpoints.cs`.
  - Compare with current license endpoints.
  - Keep activation/generation guarded and Admin/SaaS-only.
- [ ] Review `backend/Garmetix.Api/Licensing/LicenseEnforcementMiddleware.cs`.
  - Ensure API health, login, setup, factory reset, backup, and SuperAdmin routes are not accidentally blocked.
  - Confirm subscription expiry response is user-friendly and logged in message logs.
- [ ] Review `backend/Garmetix.Api/appsettings.Development.json`.
  - Port only safe local CORS additions.
  - Keep production host/origin values environment-driven.

## Priority 2 - Modular Admin/SaaS App

- [ ] Review `frontend/modular/apps/admin/pages/saas-manager.vue`.
  - Extract useful tenant management UI only after backend SaaS contracts are accepted.
- [ ] Review `frontend/modular/apps/admin/pages/sales.vue`.
  - Clarify whether this is SaaS sales, store sales, or subscription sales.
- [ ] Review `frontend/modular/apps/admin/pages/subscription.vue`.
  - Align with current license/status pages.
  - Keep destructive/activation actions guarded.
- [ ] Review Admin `nuxt.config.ts`, `package.json`, and `pages/index.vue`.
  - Do not create port conflict with existing Admin app unless route registry is updated.
  - Keep admin app base path `/admin/` for SRP deployment.

## Priority 3 - Inventory Modular App Proposal

- [ ] Review `frontend/modular/apps/inventory/` in Version6.A.
  - Decide whether Inventory becomes a separate modular app or remains Back Office route for now.
  - Verify Product, Category, Brand, Barcode, Stock Movement, Stock Operation page parity with legacy.
  - Confirm product autocomplete and barcode fixes from legacy v4.12.69 are represented.
  - Add route registry entries only after app shell, auth, and API client are aligned.
- [ ] If accepted, create Codex Stage for Inventory after Books acceptance.
  - Suggested stage: `Stage 14D Inventory Modular Parity`.

## Priority 4 - Books Differences

- [ ] Review Version6.A changes to:
  - `frontend/modular/apps/books/pages/accounting.vue`
  - `frontend/modular/apps/books/pages/parties.vue`
  - `frontend/modular/apps/books/pages/petty-cash.vue`
  - `frontend/modular/apps/books/pages/cash-details.vue`
  - `frontend/modular/apps/books/pages/vouchers.vue`
  - `frontend/modular/apps/books/pages/index.vue`
- [ ] Review added `frontend/modular/apps/books/pages/trial-balance.vue`.
  - Codex currently has accounting/trial-balance read coverage through accounting APIs; decide if separate page should be added.
- [ ] Do not port Version6.A deletion of:
  - `audit.vue`
  - `commercial-notes.vue`
  - `credit-notes/*`
  - `debit-notes/*`
  - `gst-*`
  - `vendor-*`
- [ ] If any Version6.A Books code is useful, port it page-by-page into current Codex Books structure.
- [ ] Verify all Books changes with:
  - `npm.cmd run modular:books:accounting-contract`
  - `npm.cmd run modular:books:ledger-sync-readiness`
  - `npm.cmd run modular:books:bank-reconciliation-parity`
  - `npm.cmd --prefix frontend/modular --workspace @garmetix/books-web run build`

## Priority 5 - HR Differences

- [ ] Review Version6.A changes to:
  - `frontend/modular/apps/hr/pages/attendance/today.vue`
  - `frontend/modular/apps/hr/pages/attendance/monthly.vue`
  - `frontend/modular/apps/hr/pages/attendance/payroll-review.vue`
  - `frontend/modular/apps/hr/pages/attendance/salary-draft.vue`
  - `frontend/modular/apps/hr/pages/payroll.vue`
- [ ] Do not port deletion of hardware/kiosk related pages without user approval.
  - `biometric-enrollment.vue`
  - `devices.vue`
  - `photo-review.vue`
  - other hardware stubs
- [ ] Reject hardcoded live route proxy and SSL bypass in HR `nuxt.config.ts`.
  - Use env-based API URLs.
  - Keep certificate validation intact.
- [ ] Review root/nested route renaming for Nuxt layout trapping.
  - Port only if current Codex HR still has nested route render problems.
- [ ] Review added `pages/hr-benefits/`.
  - Codex already has HR Benefits route; compare feature gaps only.
- [ ] Verify HR after any port with:
  - `npm.cmd run modular:hr:attendance-contract`
  - `npm.cmd run modular:hr:salary-payment-preview-contract`
  - `npm.cmd run modular:hr:final-closure`
  - `npm.cmd --prefix frontend/modular --workspace @garmetix/hr-web run build`

## Priority 6 - POS Differences

- [ ] Review Version6.A POS changes to:
  - `frontend/modular/apps/pos/pages/sale.vue`
  - `frontend/modular/apps/pos/app.vue`
  - `frontend/modular/apps/pos/nuxt.config.ts`
  - `frontend/modular/apps/pos/package.json`
- [ ] Inspect B2B GSTIN validation implementation.
  - Ensure it does not break fast cashier flow.
  - Validate GSTIN logic against backend customer/sale DTOs.
- [ ] Inspect returns and wholesale pricing logic.
  - Compare with Codex POS return/exchange/held-bill work.
- [ ] Review added `frontend/modular/apps/pos/pages/history.vue`.
  - Decide if Sales History belongs in POS or Back Office.
- [ ] Verify POS after any port with:
  - `npm.cmd run modular:pos:live-sale-acceptance`
  - `npm.cmd run modular:pos:final-closure`
  - `npm.cmd --prefix frontend/modular --workspace @garmetix/pos-web run build`

## Priority 7 - Shared Packages

- [ ] Review `frontend/modular/packages/shared-ui/composables/`.
  - Check if `useAuth`, `useGarmetixApi`, and other composables duplicate current shared-auth/shared-api packages.
  - Avoid moving API/auth logic into shared-ui if it belongs in shared-api or shared-auth.
- [ ] Review `frontend/modular/packages/shared-ui/utils/`.
  - Port only generic UI-safe helpers.
- [ ] Review `frontend/modular/packages/shared-ui/components/ModularAppShell.vue`.
  - Compare sidebar changes with current Codex shell and route registry.
  - Keep public app links absolute/base-aware.
- [ ] Review `frontend/modular/packages/shared-types/src/index.ts`.
  - Port stable DTO/type additions when matching backend contracts.
- [ ] Review package JSON dependency changes.
  - Avoid unnecessary dependency churn.
  - Keep Nuxt UI and workspace versions aligned with Codex Version6.

## Conflict Watchlist

- [ ] Version6.A uses Nuxt 3 wording in its notes, while Codex Version6 is Nuxt 4 / Nuxt UI 4.9 direction. Confirm actual package versions before copying code.
- [ ] Version6.A hardcoded `https://srp.aadwikafashion.in/api/**` proxy in HR. Codex should keep env-based API configuration.
- [ ] Version6.A bypassed SSL verification. Do not port this.
- [ ] Version6.A deleted Books and HR pages that Codex currently keeps for parity and route coverage. Do not port deletions.
- [ ] Tenant global filters may affect live data. Treat as backend schema/security project, not a quick UI merge.

## Suggested Codex Stages

- [ ] `Stage 14C.4` - Finish Books reconciliation closure and trial balance route review.
- [ ] `Stage 14C.5` - Books party/vendor/GST/commercial note parity review against Version6.A without deleting current pages.
- [ ] `Stage 14D.1` - Inventory modular app audit from Version6.A.
- [ ] `Stage 14D.2` - Inventory route registry and shell integration.
- [ ] `Stage 14D.3` - Product/category/brand/barcode pages.
- [ ] `Stage 14D.4` - Stock movement and stock operation parity.
- [ ] `Stage 14E.1` - POS Version6.A diff review for GSTIN, wholesale pricing, returns and history.
- [ ] `Stage 14F.1` - Admin/SaaS tenant/subscription backend design review.
- [ ] `Stage 14F.2` - Admin/SaaS UI pages after backend contracts are accepted.
- [ ] `Stage 14G.1` - Shared package cleanup after module ports stabilize.

## Acceptance Checklist For Each Ported Item

- [ ] Run focused module validation.
- [ ] Run affected app build.
- [ ] Confirm no raw server URL is shown in user messages.
- [ ] Confirm API base URLs remain env-driven.
- [ ] Confirm no internal flags are exposed to users.
- [ ] Confirm live data is not mutated by validation unless explicitly approved.
- [ ] Commit to `version6` with version/stage details.
- [ ] Deploy to `.127` only after local validation and build pass.

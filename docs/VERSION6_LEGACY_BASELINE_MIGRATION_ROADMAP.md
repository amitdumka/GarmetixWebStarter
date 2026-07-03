# Garmetix Version6 Legacy Baseline Migration Roadmap

Date: 2026-07-03  
Working branch: `version6`  
Incoming legacy package: `GarmetixWebStarter-v4.12.69-stage11d154-stock-operation-product-autocomplete.zip`  
Extracted review path: `C:\gmxv4\GarmetixWebStarter-v4.12.69-stage11d154-stock-operation-product-autocomplete`

## Purpose

Modular frontend development is paused. The uploaded v4.12.69 legacy package is now treated as the candidate current legacy baseline because it includes later Stage 11D work and the user reports that pending legacy work has been completed there.

The Version6 task is to replace the current older `legacy` application with this latest legacy baseline, then carefully re-apply only the backend/API changes from Version5 modular work that are still useful and not already superseded by v4.12.69.

No source replacement has been performed yet in this analysis step.

## Current Findings

- Current repo branch before this work was `Version5`; a new local branch `version6` was created for this migration track.
- Current repo root has:
  - `legacy/` containing the older legacy app.
  - `modular/` containing Version5 modular frontend work.
  - root `package.json` with Version5 scripts for legacy and modular validation.
- Uploaded v4.12.69 package has the full legacy shape:
  - `apps/`
  - `backend/`
  - `frontend/`
  - `deploy/`
  - `docs/`
  - `infra/`
  - `scripts/`
  - `tools/`
  - release notes `RELEASE-v4.12.47.txt` through `RELEASE-v4.12.69.txt`

Source-like comparison excluding generated/build folders such as `node_modules`, `.nuxt`, `dist`, `bin`, `obj`, `.output`, `test-artifacts`, and `backups`:

- Current `legacy`: 988 source-like files.
- Incoming v4.12.69: 1424 source-like files.
- Incoming-only source-like files: 440.
- Current-only source-like files: 4.

Current-only source-like files are:

- `backend/Garmetix.Api/Billing/PosHeldBillEndpoints.cs`
- `backend/Garmetix.Domain/Generated/Models/Inventory/PosHeldBill.cs`
- `backend/Garmetix.Infrastructure/Data/Migrations/20260622145928_Initial.cs`
- `backend/Garmetix.Infrastructure/Data/Migrations/20260622145928_Initial.Designer.cs`

## Incoming v4.12.69 Legacy Changes Understood

The uploaded package includes Stage 11D work from v4.12.47 through v4.12.69. The major areas added or changed are:

- Dot-matrix bridge, queue, spooler, admin controls, live transaction printing, Epson LX line-feed fixes, and dot-matrix documentation.
- Goods return policy and goods return acceptance validation.
- Customer dues and vendor payable reconciliation.
- Financial year closeout dashboard and owner closeout command center.
- Bank reconciliation closure.
- Profit/loss invoice item reporting.
- Stock valuation closure.
- Production go-live master acceptance, final owner sign-off, host build QA, and production build fixes.
- Purchase return advanced settlement.
- Vyapar sale import and purchase invoice import workflows.
- Digital bill CRM, WhatsApp bill delivery, marketing pages, and related backend models/services.
- Price tags, print acceptance, invoice replacement audit, sale review, and sale billing final QA.
- Stock operation product autocomplete in v4.12.69.

The incoming frontend has many additional pages, including:

- `billing/sale-review.vue`
- `billing/vyapar-import.vue`
- `billing/vyapar-import-batches.vue`
- `billing/vyapar-imported.vue`
- `bank-reconciliation-closure/index.vue`
- `financial-year-closeout/index.vue`
- `owner-closeout-command-center/index.vue`
- `goods-return-acceptance/index.vue`
- `inventory/stock-valuation-closure.vue`
- `purchase/import.vue`
- `purchase/import-acceptance.vue`
- `purchase/vendor-payable-reconciliation.vue`
- `purchase-return/advanced-settlement.vue`
- `reports/profit-loss.vue`
- `dot-matrix-print/index.vue`
- `marketing/*`
- `production-go-live-master-acceptance/index.vue`
- `production-host-build-qa/index.vue`
- `final-owner-signoff/index.vue`

## Version5 Backend/API Changes To Review For Carry Forward

The following Version5 backend commits touched `legacy/backend` after `origin/version4`:

- `c25a4c0` Stage 12D.2 AI Sense analytics endpoints.
- `62631f3` Stage 12E.8 Books audit controls.
- `53ed800` Version 5.13.13 POS server held bills.
- `ea48ffd` Version 5.13.15 POS held bill browser acceptance.

Comparison against incoming v4.12.69:

- AI Sense endpoints are present in current Version5 `DashboardEndpoints.cs` but not in v4.12.69.
- Accounting audit/message-log helper endpoints are present in current Version5 `AccountingEndpoints.cs` but not in v4.12.69.
- POS held-bill backend files are current-only and not present in v4.12.69.
- v4.12.69 has much newer billing, dashboard, accounting, database repair, migrations, and program startup code, so direct merge of older Version5 files would be risky.

Carry-forward rule:

- Start from incoming v4.12.69 files.
- Re-apply Version5 backend additions as small patches only where still needed.
- Do not overwrite v4.12.69 files with older Version5 versions.

## Proposed Version6 Stages

### V6.0 Analysis And Branch Setup

Status: in progress.

Tasks:

- Extract incoming v4.12.69 zip to a short Windows path to avoid path-length extraction errors.
- Compare source/config/docs against current `legacy`.
- Create this roadmap.
- Keep current repo clean before replacing files.

### V6.1 Replace Legacy Baseline

Goal: make `legacy/` match v4.12.69 as the latest monolithic legacy system.

Tasks:

- Replace `legacy/apps`, `legacy/backend`, `legacy/frontend`, `legacy/deploy`, `legacy/docs`, `legacy/infra`, `legacy/scripts`, `legacy/tools`, and root legacy docs from the incoming package.
- Exclude generated/build/runtime folders:
  - `node_modules`
  - `.nuxt`
  - `.output`
  - `dist`
  - `bin`
  - `obj`
  - `test-artifacts`
  - `backups`
- Do not copy live `secrets/` into git without explicit approval.
- Review incoming `.env.example` and `.env.production.example` only; do not copy private `.env` values.
- Remove old current-only initial migrations if v4.12.69 migrations become the baseline.

Validation:

- `dotnet build legacy/backend/Garmetix.Api/Garmetix.Api.csproj -c Release`
- `npm --prefix legacy/frontend/garmetix-web run build`

### V6.2 Re-Apply Required Backend/API Carry-Forward

Goal: bring useful Version5 backend work into v4.12.69 without downgrading latest legacy code.

Candidate patches:

- AI Sense analytics API:
  - Prefer a new dedicated endpoint file, for example `Garmetix.Api/AiSense/AiSenseEndpoints.cs`.
  - Reuse v4.12.69 dashboard/business summary types and queries.
  - Register from `Program.cs`.
- Books/accounting audit API:
  - Re-add `/api/accounting/audit/recent`, `/api/accounting/audit/events/{id}`, and `/api/accounting/message-logs` only if Books modular or legacy pages need them.
  - Keep v4.12.69 accounting code as base.
- POS held bills:
  - v4.12.69 does not include current Version5 held-bill files.
  - Confirm whether held bills should exist in legacy Version6 now or wait until modular resumes.
  - If required, add as a separate endpoint/model/migration patch after v4.12.69 builds.

Validation:

- API build.
- Focused endpoint smoke checks.
- Existing frontend build.

### V6.3 Root Workspace And Script Alignment

Goal: keep Version6 usable while modular work is paused.

Tasks:

- Update root `package.json` version from Version5 numbering to a Version6 migration version.
- Keep legacy scripts working:
  - `legacy:api:build`
  - `legacy:api:test`
  - `legacy:web:build`
- Keep modular scripts present but mark modular development paused in docs.
- Ensure root README explains:
  - `legacy/` is v4.12.69 baseline.
  - `modular/` is paused Version5 work to be resumed later.

Validation:

- `npm run legacy:api:build`
- `npm run legacy:web:build`
- Run tests if dependencies/database allow.

### V6.4 Deployment And Environment Review

Goal: avoid breaking existing Windows, Ubuntu, Docker, and Cloudflare deployment.

Tasks:

- Compare incoming `docker-compose.yml`, `docker-compose.prod.yml`, `deploy/`, and `infra/` with current repo scripts.
- Preserve useful Version5 SRP deployment knowledge separately; do not mix modular SRP deployment into legacy baseline unless needed.
- Ensure API/frontend environment variables still use config and do not hardcode production domains.
- Keep Cloudflare and server secrets out of committed files.

Validation:

- Docker compose config validation.
- API health endpoint smoke check.
- Frontend login/API base check.

### V6.5 Resume Modular Planning Later

Goal: after Version6 legacy baseline is stable, decide how to continue modular apps.

Tasks:

- Re-map modular route ownership against v4.12.69 because legacy now has many new pages.
- Rebuild shared API contracts from the newer backend.
- Avoid carrying obsolete Version5 frontend assumptions.
- Continue modular app split only after legacy v4.12.69 baseline is committed and buildable.

## Immediate TODO List

- Confirm whether to proceed with replacing `legacy/` from v4.12.69 now.
- Confirm whether POS held bills must be preserved immediately in Version6.
- Confirm whether AI Sense and Books audit endpoints are required immediately after the legacy baseline replacement.
- Perform the baseline replacement on branch `version6`.
- Validate API build.
- Validate frontend build.
- Commit the Version6 baseline migration.
- Push `version6` after successful validation, if remote push is desired.

## Risks

- The incoming package contains build/runtime/generated folders and a `secrets/` directory. These must not be blindly copied into git.
- v4.12.69 has newer migrations than the current legacy baseline. Database upgrade/reset behavior must be tested before production use.
- Directly merging current Version5 backend files would likely regress v4.12.69 because billing, dashboard, accounting, and program startup changed heavily.
- Modular apps were built against older legacy APIs. They should be treated as paused until the v4.12.69 API surface is confirmed.
- Incoming README appears older than the release notes, so release notes and source code should be treated as more reliable than README version text.

## Recommended Decision

Proceed with V6.1 by replacing the current `legacy/` source with the v4.12.69 package, excluding generated artifacts and secrets. Then apply V6.2 carry-forward patches only for AI Sense, Books audit/message logs, and POS held bills if confirmed.

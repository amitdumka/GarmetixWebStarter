Note: All Claude work and instruction log here. Full detail lives in `.claude/` (profile, environment, standing instructions, learnings, roadmap, todo, changelog) - this file is the short pointer/summary for Codex.

## 2026-07-07 - Initial analysis + `.claude/` setup

Claude ran a full first-pass analysis of the repo (backend, `frontend/legacy`, `frontend/modular`), read the existing Codex/AntiGravity TODOs and `MODULAR_TODO.md` stage history, and created the `.claude/` working-memory folder. No application code was changed.

Bugs found (detail + file:line in `.claude/todo.md` and `.claude/changelog.md`):
- Factory reset endpoint is Admin-gated, not SuperAdmin-gated (`backend/Garmetix.Api/Backup/FactoryResetEndpoints.cs`).
- A few `backend/Garmetix.Domain/Generated/` models have acknowledged-incomplete TODOs (Invoicing stored-vs-calculated fields, Fabric/UOM, basic rate calculator, a card-type enum).
- `.gitattributes` has no blanket line-ending rule, causing CRLF/LF diff noise across OSes (cosmetic, not functional).

Confirmed clean: no disabled TLS verification, no hardcoded production domains in modular source, no stray TODO/FIXME/console.log in modular apps/packages, modular structure validation (`node frontend/modular/scripts/validate-structure.mjs`) passes.

Roadmap/todo: see `.claude/roadmap.md` and `.claude/todo.md`. Near-term open items are Books Stage 14C.5 closure, the Stage 14F.7 MCP wrapper, clearing the cross-module "pending live evidence" backlog, and the factory-reset security fix.

## 2026-07-07 - Books Stage 14C.5 closure (GST report finalization, financial-year lock acceptance)

Closed the Books Version6 modular parity lane (bumped `frontend/modular/config/version.ts` to `6.0.52`). No backend/DB changes.

- Confirmed `gst-reports.vue`, `gst-returns.vue`, `gst-production.vue` CSV/JSON/Excel/schema-review export buttons already call the live `GstReturns`/`Gstin` backend services (export handoff was already real, not a stub).
- Fixed `frontend/modular/apps/books/pages/gst-production.vue`: the GSTIN provider status call is Admin-policy gated on the backend, so it now renders "Admin only" for non-Admin accountant roles instead of a generic load-failure error.
- Added guarded financial-year lock create/update and unlock UI to `frontend/modular/apps/books/pages/financial-year-locks.vue` (confirmation phrases `LOCK FINANCIAL YEAR` / `UNLOCK FINANCIAL YEAR`, matching the existing Books guarded-write pattern), calling the pre-existing `POST accounting/financial-year-locks` and `POST accounting/financial-year-locks/{id}/unlock` endpoints. This page was previously read-only by design; Amit confirmed adding the write UI was in scope for this stage.
- Added `frontend/modular/scripts/books-stage14c5-closure.mjs` (non-mutating closure gate, GO/CONDITIONAL evidence pattern mirrored from `hr-final-closure.mjs`), matching `modular:books:stage14c5-closure` / `books:stage14c5-closure` npm commands, and `frontend/modular/docs/stage-14c5-books-gst-fy-lock-closure.md`.
- Wired the new script into `validate-structure.mjs` and `validate-all.mjs`; updated `frontend/modular/docs/MODULAR_TODO.md` (14C.5 marked complete, Books lane closed).
- Explicitly out of scope by Amit's choice: no new modular page for the legacy-only `GET /api/financial-year-closeout` evidence endpoint (script/doc only, no UI port).
- Validated: `node frontend/modular/scripts/validate-structure.mjs`, `node frontend/modular/scripts/books-stage14c5-closure.mjs`, `node frontend/modular/scripts/books-stage13d-closure.mjs` (regression), `node frontend/modular/scripts/books-accounting-readiness.mjs` (regression), `npm --prefix frontend/modular --workspace @garmetix/books-web run build` (all pass).

## 2026-07-07 - Books Stage 14G: full legacy GST menu parity + SRP deploy

Amit asked to port every page under legacy's "GST" nav group with no feature left behind (14C.5 only covered export/FY-lock, not the full builder), using `frontend/legacy/garmetix-web` and root `legacy/docs/` for reference, then deploy to `192.168.11.127` (SRP). Bumped modular version to `6.0.53`. No backend/DB changes - all needed endpoints already existed.

- Rewrote `frontend/modular/apps/books/pages/gst-returns.vue` from a 331-line read-only summary into the full manual GSTR-1/GSTR-3B builder: header form, dynamic B2B/B2C/HSN/document/nil-rated rows (GSTR-1), fixed supplies/ITC/interstate/inward/interest sections (GSTR-3B), Preview, Load From Books, Save/Update/Delete/Mark-Filed draft lifecycle, ad-hoc JSON/Excel export, draft audit trail, GST accounting-posting bridge (typed `POST GST ACCOUNTING` confirmation - stronger than legacy's plain `confirm()`, matching the modular ledger-posting convention), and a Review & Send to CA modal (email/WhatsApp share).
- Ported `useGstReviewContact` composable to `apps/books/composables/` (CA contact + share log, localStorage-backed) and added the same CA-share modal to `gst-reports.vue`.
- Added `accounting-gst-validation.vue` (new Books route, `moduleKey: gst`) porting the post-import validation report, and `gst-final-acceptance.vue` (Admin app) - this filled a previously **dangling route** in `routes.ts` that had no page file behind it.
- Extended `books-api.ts`'s `download()` to support an optional POST body (needed for ad-hoc GST export); found and fixed two real bugs via manual dev-server browser testing (not just builds): a `<USelect>` crash from an empty-string fallback option value in `gst-returns.vue`, and an off-by-one date bug in `accounting-gst-validation.vue`'s default month range caused by a UTC/local timezone conversion.
- Added `books-stage14g-closure.mjs`; discovered and fixed a durability bug in the *existing* `books-stage14c5-closure.mjs` (it asserted an exact `stage.includes('Stage 14C.5')` string match and two now-superseded UI-copy markers, which would break forever on any later version bump - removed the brittle stage-string check and the stale markers).
- Deployed to SRP (`192.168.11.127`): no `sshpass` was available and no SSH key was trusted yet, so (with Amit's explicit approval each step) generated a dedicated SSH keypair, had Amit run one interactive `ssh-copy-id`-equivalent command to trust it, then ran the real `srp-whole-site-deploy.sh` (forcing Git Bash via `GARMETIX_PREFER_WSL=false` since the key was set up there, not in WSL's separate filesystem). Also fixed a stale `SRP_API_PROJECT` path in the local (out-of-repo) deploy config. Verified live via direct LAN checks and `srp-public-acceptance.mjs --live` (all apps + API health pass on both public and LAN).
- Found and flagged (not fixed, out of scope, spawned as a separate background task) a **pre-existing, unrelated** CRM bug: `/crm/customers` build-collides with its own `customers/new` and `customers/dues-reconciliation` sub-routes during static prerendering, breaking that page live. Not caused by this session's changes.
- Validated: `node frontend/modular/scripts/validate-structure.mjs`, `books-accounting-contract-check.mjs`, `books-stage14g-closure.mjs`, `books-stage14c5-closure.mjs` (regression, post-fix), `books-stage13d-closure.mjs` (regression), clean `books-web` and `admin-web` production builds, manual browser testing via dev server, and the live SRP acceptance script.


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


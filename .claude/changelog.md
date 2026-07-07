# Claude Changelog

Append-only. Newest entry on top. Format: date, session summary, files touched, reasoning.

---

## 2026-07-07 - Books Stage 14C.5 closure (GST report finalization, financial-year lock acceptance)

**Type**: modular frontend feature + validation scripts/docs. No backend/DB changes.

**What happened**: Closed the last open core-module lane per `frontend/modular/docs/MODULAR_TODO.md` (Books Stage 14C.5). Explored existing GST pages (`gst-reports.vue`, `gst-returns.vue`, `gst-production.vue`), the financial-year-lock page, the `GstReturns`/`Gstin` backend services, and the `books-stage13d-closure.mjs`/`hr-final-closure.mjs` templates via a background research agent before making changes. Confirmed Amit's scope decisions via AskUserQuestion: (1) add guarded financial-year lock create/unlock UI (previously read-only by design) rather than leave it read-only, and (2) keep the Books final closure gate to a script/doc only, not a new modular page for the legacy-only financial-year-closeout endpoint.

**Files updated**:
- `frontend/modular/apps/books/pages/financial-year-locks.vue` - added guarded lock create/edit form (`LOCK FINANCIAL YEAR` confirmation) and unlock action (`UNLOCK FINANCIAL YEAR` confirmation) calling the pre-existing `POST accounting/financial-year-locks` and `POST accounting/financial-year-locks/{id}/unlock` endpoints. Scope (company vs. current store) derived the same way vouchers/bank-transactions already do (`setup/status` + `stores`), no new endpoints or DTOs needed.
- `frontend/modular/apps/books/pages/gst-production.vue` - GSTIN provider status (Admin-policy gated backend) no longer surfaces as a generic load failure for non-Admin accountant roles; now shows "Admin only" in the readiness/provider cards.
- `frontend/modular/config/version.ts` - bumped to `6.0.52`, Stage 14C.5.
- `frontend/modular/docs/MODULAR_TODO.md` - marked 14C.5 complete, closed the Books modular parity lane, updated forward references.

**Files added**:
- `frontend/modular/scripts/books-stage14c5-closure.mjs` - non-mutating closure gate (structural completeness + GST/FY-lock page marker checks + GO/CONDITIONAL live-evidence gate via `GARMETIX_SMOKE_AUTH_TOKEN` / `GARMETIX_BOOKS_GST_FY_LOCK_MANUAL_ACCEPTANCE`, mirroring `hr-final-closure.mjs`).
- `frontend/modular/docs/stage-14c5-books-gst-fy-lock-closure.md` - stage doc, mirrors `stage-13d-final-books-closure.md` template.
- npm commands `modular:books:stage14c5-closure` (root `package.json`) / `books:stage14c5-closure` (`frontend/modular/package.json`), wired into `validate-structure.mjs` and `validate-all.mjs`.

**Validation**: `node frontend/modular/scripts/validate-structure.mjs`, `node frontend/modular/scripts/books-stage14c5-closure.mjs` (CONDITIONAL - pending live token/manual evidence, as expected), `node frontend/modular/scripts/books-stage13d-closure.mjs` and `books-accounting-readiness.mjs` (regression, both pass), `npm --prefix frontend/modular --workspace @garmetix/books-web run build` (clean build, all GST/FY-lock routes prerender).

**Remaining/deferred** (see updated `.claude/roadmap.md`/`.claude/todo.md`): GST draft lifecycle write endpoints (save/filed/send-review/accounting-posting) remain backend-complete but frontend-unused; the legacy-only `/api/financial-year-closeout` evidence endpoint has no modular page by explicit choice; live-token/manual-acceptance evidence for the new guarded lock/unlock actions is still pending.

## 2026-07-07 - Initial project analysis + `.claude/` setup

**Type**: documentation/analysis only, no application code changed.

**What happened**: First Claude session on this project. Read `README.md`, `docs/codex-workspace-instructions.md`, `AntiGarvity2CodeTODO.md`, `.codex/Assistant_MCP_AI_Sense_TODO.md`, `.codex/Priority6_POS_CRM_TODO.md`, `frontend/modular/docs/MODULAR_TODO.md`, `docs/VERSION6_LEGACY_BASELINE_MIGRATION_ROADMAP.md`, root `package.json`, `frontend/modular/config/version.ts`. Ran `node frontend/modular/scripts/validate-structure.mjs` (passed). Grepped backend + modular frontend for TODO/FIXME, disabled-TLS patterns, hardcoded prod domains, disabled-auth patterns, stray console.log. Verified the `git status` "1,689 modified files" reading was a CRLF/LF false positive (`git diff --ignore-space-at-eol` = empty on sampled files).

**Files added**:
- `.claude/README.md` - folder index.
- `.claude/profile.md` - Claude's role/identity in this multi-agent project (Codex + Antigravity + Claude).
- `.claude/environment.md` - workspace map, repo layout, deploy targets, validation commands, CRLF quirk note.
- `.claude/instructions.md` - standing change-boundary and process rules Claude must follow.
- `.claude/learnings.md` - non-obvious codebase facts (dual "legacy" folders, generated domain models, factory-reset gap, script naming convention, assistant feature flag).
- `.claude/roadmap.md` - forward-looking plan (Books 14C.5, MCP wrapper, live-evidence backlog, security follow-up, AntiGravity port review, `.gitattributes` fix).
- `.claude/todo.md` - actionable bug list + follow-up work + process reminders.
- `.claude/changelog.md` - this file.

**Files updated**:
- root `CLAUDE.md` - replaced placeholder text with pointer to `.claude/` and a summary of this session (see that file for exact wording).

**Bugs found** (see `.claude/todo.md` for full detail, filed 2026-07-07):
- `backend/Garmetix.Api/Backup/FactoryResetEndpoints.cs:17` - factory reset is Admin-gated, not SuperAdmin-gated (previously flagged in Stage 13F closure notes, still open).
- `backend/Garmetix.Domain/Generated/Models/Inventory/Invoicing.cs:289-290` - stored fields that a code comment says should be calculated instead; missing `JsonIgnore`.
- `backend/Garmetix.Domain/Generated/Models/Inventory/Inventory.cs:39,108` - Fabric/UOM support and a "Basic Rate Calculator" toolkit function are acknowledged as not yet implemented.
- `backend/Garmetix.Domain/Generated/Enums/BharatEnums.cs:58` - card-type enum explicitly marked incomplete.
- No `* text=auto` rule in root `.gitattributes` - causes CRLF/LF diff noise across OSes/tools (not a functional bug, but a recurring false alarm worth a one-line fix).

**Confirmed clean** (checked, no issue): no disabled TLS verification, no hardcoded production domains in modular source, no stray TODO/FIXME/console.log in modular apps/packages, modular structure validation passes.

**Why this session happened**: Amit asked for a full project analysis, bug scan, and roadmap/todo, plus a `.claude/` working-memory setup per project instructions so Codex can stay in sync with Claude's work going forward.

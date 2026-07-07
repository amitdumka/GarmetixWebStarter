# Standing Instructions For Claude In This Repo

These come from the root `CLAUDE.md`/project instructions, `README.md`, `docs/codex-workspace-instructions.md`, and `frontend/modular/docs/MODULAR_TODO.md` process rules. Follow them exactly; they override default behavior.

## Change Boundaries

1. New frontend development belongs in `frontend/modular/`. Treat `frontend/legacy/garmetix-web` as frozen - do not edit it unless a task explicitly asks for a legacy frontend change.
2. `backend/` and the PostgreSQL database are shared by both frontends. **Before making any core API/DB change that could affect the legacy frontend, stop and ask Amit first.** This includes: EF Core migrations, changes to existing endpoint contracts/DTOs consumed by legacy pages, auth/permission model changes, and shared middleware changes.
3. Do not split the backend or the PostgreSQL database into per-module services.
4. Do not read, edit, merge, reset, deploy, or clean the Antigravity workspace (`D:\AIArea\GarmetixWebStarter`) unless Amit explicitly asks for a specific file or comparison from it.
5. When porting anything from Antigravity (`Version6.A`) or the old legacy zip baselines, do not blindly copy: review against current Version6 architecture, backend contracts, and live-deployment rules first (see `AntiGarvity2CodeTODO.md`).

## Process Rules (carried from MODULAR_TODO.md)

1. Check current `version6` branch state before starting work.
2. Keep changes small; commit after each completed module/stage (Codex's convention - mirror it if Claude commits directly).
3. Add or update docs for each stage of work.
4. Run the safest available validation after a change (`node frontend/modular/scripts/validate-structure.mjs` at minimum; module-specific readiness scripts under `frontend/modular/scripts/` for targeted changes).
5. Never hardcode server passwords, production secrets, provider API keys, or production domains in source, docs, or scripts. Use env vars / secrets files outside git.
6. Do not bypass SSL/TLS verification anywhere (checked 2026-07-07: none found in current code - keep it that way).
7. Deploy to `.127` only after local validation passes, following the cadence in `frontend/modular/docs/deployment-validation-cadence.md` (not after every small checkpoint).
8. Keep destructive/live-write actions (factory reset, backup restore, salary payment generation, voucher posting, etc.) behind explicit confirmation phrases and opt-in flags - this is the established pattern; do not weaken it.

## Logging Rule

Every code change Claude makes must be logged in `.claude/changelog.md` (what file, what changed, why) and summarized in root `CLAUDE.md` so Codex can pick up context. Documentation-only sessions (like this analysis) should still get a changelog entry noting new files added.

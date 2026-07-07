# Garmetix Roadmap (Claude View)

Last updated: 2026-07-07. Source of truth for stage-by-stage history remains `frontend/modular/docs/MODULAR_TODO.md` (Codex-owned); this file is Claude's forward-looking synthesis, not a replacement.

## Where Things Stand

- Modular version: `6.0.52`, Stage 14C.5 (Books GST report finalization and financial-year lock acceptance), just completed - closes the Books modular parity lane.
- All six modular apps (`main`, `pos`, `hr`, `books`, `ai-sense`, `admin`) plus `crm` have reached at least one "final closure" gate. Legacy frontend and shared backend remain the single source of truth; modular apps consume the same API/DB.
- Structure validation (`node frontend/modular/scripts/validate-structure.mjs`) passes as of this session.

## Near-Term (next 1-3 sessions)

1. ~~**Books 14C.5**~~ - closed 2026-07-07: GST/accounting report finalization confirmed, financial-year lock create/unlock UI added, final Books closure gate script/doc shipped. Books modular parity lane is done pending live-token/manual evidence. See `.claude/changelog.md`.
2. **Assistant 14F.7** - MCP wrapper around the existing read-only AI Sense tool catalog, additive only, local/private-tunnel auth until reviewed.
3. **Live-evidence backlog** - a recurring pattern across POS, HR, Books, CRM and Admin closures: code and dry-run validation are done, but real production evidence (manual browser acceptance, live-token smoke tests, actual cashier/payroll runs) is still marked conditional. Worth a dedicated pass to clear this backlog rather than letting each module's "pending live evidence" note linger indefinitely. Books 14C.5's new guarded lock/unlock actions and GST closure gate now also carry this same "pending live evidence" flag.
4. **Security follow-up** - restrict factory reset to SuperAdmin only (currently Admin-policy gated; see `learnings.md` and bug list below). Low effort, meaningful risk reduction.

## Medium-Term

5. **AntiGravity port review** (`AntiGarvity2CodeTODO.md`, Priority 0-7) - none of these items are checked off yet. Highest-value candidates to review first:
   - Priority 1: SaaS tenant/subscription backend model (`Tenant`, `Subscription`, `GarmetixDbContext` global query filter) - security/architecture-sensitive, needs careful review before any tenant isolation logic touches shared live data.
   - Priority 3: Inventory as its own modular app (currently inventory lives inside `main`) - would let `main` shrink further toward "lean back office," matching the Stage 12G goal.
   - Priority 4/5/6: Books/HR/POS page-level diffs from Version6.A - smaller, page-by-page ports.
   - Priority 7: shared package composable overlap (`shared-ui` vs `shared-api`/`shared-auth`) - cleanup, not urgent.
6. **`.gitattributes` line-ending policy** - add a blanket `* text=auto` (or explicit per-extension) rule so CRLF/LF churn doesn't produce misleading diffs across tools/OSes. Currently only `*.gitattributes` and `*.sh` are pinned to LF.

## Longer-Term / Deferred By Design

- Keeping one shared ASP.NET Core API and one PostgreSQL database is an explicit, repeated decision across every stage doc. Do not propose splitting these without an explicit user ask.
- Per-app Cloudflare subdomains (`pos.`, `hr.`, `books.`, `ai-sense.`, `admin.`) are documented but full public-domain cutover evidence is still pending per the SRP deployment stage notes.

## Known Risks Carried Forward (see `.claude/todo.md` bug list for detail)

- Factory reset authorization scope (Admin, not SuperAdmin).
- Several `Garmetix.Domain.Generated` models have acknowledged incompleteness (UOM/Fabric, basic rate calculator, Invoicing derived-vs-stored properties).
- Large backlog of unreviewed Antigravity (`Version6.A`) changes that may contain already-solved problems (e.g., Inventory app, GSTIN/wholesale pricing in POS) worth comparing before building from scratch.

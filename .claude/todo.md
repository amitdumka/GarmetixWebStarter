# Active Todo (Claude View)

Last updated: 2026-07-07. Check items off as completed; move detail/history into `changelog.md` once done. Codex's own module-lane TODOs live in `.codex/*_TODO.md` and `frontend/modular/docs/MODULAR_TODO.md` - this list is Claude-facing and bug/analysis driven.

## Bugs / Issues Found (2026-07-07 analysis pass)

- [ ] **Security**: `backend/Garmetix.Api/Backup/FactoryResetEndpoints.cs:17` gates factory reset behind `GarmetixPolicies.Admin`, not SuperAdmin. Stage 13F closure notes already flagged this as a residual risk. Confirm with Amit whether to tighten to SuperAdmin-only, and check `frontend/modular/apps/admin` writable-preflight UI matches whatever policy is chosen.
- [ ] **Data modeling**: `backend/Garmetix.Domain/Generated/Models/Inventory/Invoicing.cs:289-290` - fields are stored in DB that a `//TODO` comment says should instead be calculated, plus missing `JsonIgnore`. Worth a follow-up to confirm this isn't causing drift between stored and true values on invoices.
- [ ] **Incomplete domain model**: `backend/Garmetix.Domain/Generated/Models/Inventory/Inventory.cs:39` - Fabric/UOM (unit of measurement) support is not implemented; relevant if fabric-based (as opposed to piece-based) inventory is ever needed.
- [ ] **Incomplete domain model**: `backend/Garmetix.Domain/Generated/Models/Inventory/Inventory.cs:108` - a "Basic Rate Calculator" static toolkit function referenced as needed but not yet created; check whether rate calculation logic is duplicated ad hoc elsewhere in the codebase as a result.
- [ ] **Incomplete enum**: `backend/Garmetix.Domain/Generated/Enums/BharatEnums.cs:58` - card-type enum is explicitly marked incomplete.
- [ ] **Repo hygiene**: no `* text=auto` (or equivalent) rule in `.gitattributes`; only `*.gitattributes` and `*.sh` are pinned to LF. Everything else's line endings are undefined, which is why a Windows checkout diffs as ~1,689 "modified" files against a Linux git client. Low priority but cheap to fix and removes a recurring false alarm.
- [ ] **CRM bug (found 2026-07-07, live on SRP)**: `/crm/customers` build-collides with its own `customers/new`/`customers/dues-reconciliation` sub-routes during static prerendering (`EISDIR` on `.output/public/customers`), breaking the page live (redirect loop, no content). Unrelated to any GST work this session - flagged as a spawned background task (task_38949783), being fixed in a separate session.
- [x] **Nitro flaky cold-build bug (found + mitigated 2026-07-07)**: a from-scratch `nuxt generate` non-deterministically crashes or silently emits a 16-byte `"Redirecting..."` stub instead of real page content for every route of every modular app (confirmed unrelated to GST source changes). Mitigated with a retry+content-size-verification safety net in `frontend/modular/deploy/srp-whole-site-deploy.sh`'s `build_app()`. Underlying Nitro/Nuxt root cause not fixed upstream, just worked around.
- [x] **npm workspace install bug (found + fixed 2026-07-07)**: every app/shared-package `package.json` pinned internal `@garmetix/shared-*` deps at a stale exact version (`"6.0.0"`) not matching the real package version, causing npm 11.13 to 404 against the public registry for these private package names and fail the whole install. Fixed by relaxing all pins to `"*"`.
- [x] **Deploy script `dotnet publish` Git-Bash path bug (found 2026-07-07, fixed 2026-07-08)**: `srp-whole-site-deploy.sh`'s dotnet publish step failed with a doubled drive-letter path (`C:\c\AIArea\...`) under Git Bash. Root cause: Git Bash/MSYS's own implicit argv-to-Windows-path conversion for the native `dotnet` binary (not `wslpath`, which an earlier fix attempt wrongly targeted). Fixed via explicit `cygpath -w` in `dotnet_path_arg()`, verified against a real `--build-only` deploy run. Commit `99a835d`.

## Non-Bugs Confirmed Clean (checked, no action needed)

- [x] No `rejectUnauthorized: false` / disabled TLS verification anywhere in frontend or backend.
- [x] No hardcoded production domains found inside modular app source (`frontend/modular/apps`, `frontend/modular/packages`).
- [x] No stray `TODO`/`FIXME`/`console.log` left in modular `apps`/`packages` Vue/TS source.
- [x] `node frontend/modular/scripts/validate-structure.mjs` passes.

## Follow-Up Work (from roadmap, actionable slice)

- [x] Books Stage 14C.5 (GST/accounting report finalization, financial-year lock acceptance, final Books closure) - closed 2026-07-07. See `.claude/changelog.md` for detail. Books modular parity lane is done pending live-token/manual evidence.
- [x] Books Stage 14G (full legacy GST menu parity: GSTR-1/3B builder, CA share, accounting-gst-validation, gst-final-acceptance) - closed 2026-07-07, deployed to `.127` SRP. The first deploy attempt's acceptance check falsely showed all-green (status-code-only); Amit caught the real bug from his browser, which led to fixing two real infra bugs (Nitro flaky build, npm workspace pin) and a genuinely-verified redeploy. See `.claude/changelog.md` for the full trail.
- [x] Start Stage 14F.7 MCP wrapper for the AI Sense read-only tool catalog once Assistant is confirmed stable. Done 2026-07-08: `POST /api/mcp`, gated behind `Assistant:McpEnabled` (default off), additive wrapper around `AssistantToolCatalog`, no live MCP-client test yet (no test creds/DB in this environment).
- [ ] Ask Amit whether to prioritize clearing the "pending live evidence" backlog (POS/HR/Books/CRM/Admin) over new feature work.
- [ ] Begin AntiGravity (`Version6.A`) port review starting with Priority 0 (file-list diff) from `AntiGarvity2CodeTODO.md` - do not read Antigravity workspace files beyond what's needed for a named comparison without asking first.

## Process Reminders

- [ ] Before any backend/API/DB change: confirm with Amit if it can affect `frontend/legacy/garmetix-web`.
- [ ] After any code change: update `.claude/changelog.md` and root `CLAUDE.md`.

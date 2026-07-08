# Claude Changelog

Append-only. Newest entry on top. Format: date, session summary, files touched, reasoning.

---

## 2026-07-08 - Full SRP deploy (v6.0.58): shipped MCP + Stage 14J, found and fixed a live tar-upload permission bug

**Type**: production deploy + incident found/fixed live during that deploy. Triggered by Amit's explicit "complete MCP and AI part and deploy it".

**Deploy**: ran a full `srp-whole-site-deploy.sh` (all 7 apps + API, no `--apps`/`--skip-api` filter) to ship everything accumulated since `6.0.57`: Stage 14J (CRM Digital Bills fixes, Main Sale Invoices rebuild), the `cygpath` dotnet-publish fix, and Stage 14F.7 (MCP server layer). Build-only dry run first (staged release, verified no degenerate `"Redirecting..."` stubs), then the real upload.

**Incident found live**: the upload used the tar-stream fallback (`rsync` not installed on this host) - `upload_release()` symlinks `current` to the new release but does not restart the API systemd service (that's normally done by `--install-remote`, since a running process doesn't reload a new binary from a re-pointed symlink on its own). Ran `--skip-build --install-remote` to apply that; the API then crash-looped with `status=203/EXEC`. Root cause: the tar stream, built from `$LOCAL_RELEASE` on Git Bash's NTFS-backed filesystem, does not reliably preserve the Unix executable bit (NTFS has no native equivalent) - the self-contained `Garmetix.Api` apphost landed on the remote as `-rw-r--r--`, and systemd's `ExecStart` can't exec a non-executable file, so `EXEC` errors are cosmetic-only tracebacks with no application log line - the container/service metadata (`203/EXEC`) was the only signal.

**Immediate live fix**: `chmod +x` on the deployed binary; systemd's `auto-restart` policy picked it back up on its own retry (no `sudo systemctl restart` needed - non-interactive sudo failed with no TTY, but wasn't required once the exec bit was fixed, since the service was already mid-crash-loop and auto-retries).

**Root-cause fix**: `frontend/modular/deploy/srp-whole-site-deploy.sh`'s `upload_payload()` now runs an explicit `chmod +x` on the known API apphost path (`$REMOTE_RELEASE/api/Garmetix.Api`) over SSH right after the tar-stream extraction, whenever the API is part of this deploy. Scoped to the tar-stream fallback path only (the `rsync` path already used `-a`, which does preserve permissions correctly - this bug is specific to the no-rsync fallback).

**Verification**: confirmed the API service reached `active (running)` with a healthy `/api/health` (`databaseReady: true`), confirmed `/api/mcp` correctly returns 404 (route not mapped - `Assistant:McpEnabled` defaults off in production, working as designed), ran `srp-public-acceptance.mjs --live --strict` (all-green, public + LAN, including Cloudflare-routed URLs), and spot-checked real page-body byte content (not just HTTP status) for CRM Digital Bills and the Main billing page to rule out the prior Nitro flaky-stub bug recurring - both served full real shells (~2900 bytes), no stub. Also incidentally confirmed the previously-flagged `/crm/customers` build-collision bug (`.claude/todo.md`) no longer reproduces - now serves real content with a single clean trailing-slash redirect, not a loop.

**Files touched**: `frontend/modular/deploy/srp-whole-site-deploy.sh` (`upload_payload()` chmod fix), `.claude/todo.md` (CRM customers bug marked resolved).

---

## 2026-07-08 - Stage 14F.7 MCP server layer + real fix for the Git-Bash dotnet-publish bug + Stage 14J closure

**Type**: new backend feature (MCP), deploy-tooling bug fix, prior-stage cleanup. Continuation of a session that had hit a usage limit mid-work; resumed via "resume last pending work" then "mcp and ai sense complete it along with pending with you".

**Stage 14J closure (CRM + Main, committed earlier this session, confirmed still holding on deploy)**: CRM Digital Bills got real Prev/Next pagination and a CSS clipping fix (`.garmetix-table-panel`'s shared `overflow: hidden` shorthand was silently winning over pages' own `overflow-x-auto`, scoped a fix to that class only); a hard-deleted sale invoice's `DigitalInvoice` row is now soft-deleted alongside it so the CRM list stops showing a dead link as "Active"; the Main app's stub `billing/index.vue` was rebuilt to parity with POS's `history.vue` (filters, pagination, detail slideover) against the existing paged `GET /api/billing/sales`. Still not deployed - holding per Amit's 3-iteration deploy gate.

**Git-Bash dotnet-publish doubled-path bug, fixed for real**: the earlier fix (`22988f0`) gated `wslpath` conversion behind WSL env-var checks, based on a wrong theory. Live debug tracing this turn proved `wslpath` was never even on PATH under Git Bash and the old `*dotnet.exe` guard never matched (`$DOTNET_COMMAND` resolves to plain `dotnet`) - the real corruption is Git Bash/MSYS's own implicit argv-to-Windows-path conversion for the native `dotnet` binary. Fixed `dotnet_path_arg()` in `frontend/modular/deploy/srp-whole-site-deploy.sh` to explicitly call `cygpath -w` for Git Bash/MSYS (keeping `wslpath -w` only for genuine WSL). Verified against a real `--apps=hr --build-only` run producing a correct, complete API publish output. Commit `99a835d`.

**Stage 14F.7 - MCP server layer (new)**: added the official `ModelContextProtocol.AspNetCore` SDK (0.4.0-preview.1, restores cleanly from nuget.org) and exposed the existing Assistant read-only tool catalog over MCP at `POST /api/mcp`, for external MCP clients (Claude Desktop, claude.ai) rather than just the in-app chat panel. Followed the additive-wrapper approach the code already documented (`AssistantAnthropicClient.cs`'s own comment named this exact plan): new `backend/Garmetix.Api/Assistant/AssistantMcpTools.cs` is a `[McpServerToolType]` class whose 6 methods (matching the 6 existing tools) just build a JSON args object and forward to `AssistantToolCatalog.ExecuteAsync` - zero business-logic duplication. Discovered `WorkspaceScope.ApplyTo(...)` only needs `HttpContext.User` (a `ClaimsPrincipal`), not the full request, so hosting the MCP transport inside the same ASP.NET Core pipeline (`AddMcpServer().WithHttpTransport().WithTools<AssistantMcpTools>()`, `app.MapMcp("/api/mcp").RequireAuthorization()`) means an MCP caller authenticates with the same JWT bearer token as every other endpoint and gets identical tenant scoping - no refactor of the tool catalog needed, no new auth system invented. Added a new `Assistant:McpEnabled` flag (default `false`, independent of the existing `Assistant:Enabled` chat flag, since MCP callers are a different risk surface) to `AssistantOptions.cs` and both `appsettings*.json` files. `dotnet build` succeeded on the first attempt with only pre-existing warnings - the API assumptions about the preview SDK (attribute names, `WithHttpTransport`/`WithTools<T>`/`MapMcp` signatures) were all correct on the first try. Not live-tested against a real MCP client yet (no test credentials/DB in this environment, matching this session's established backend-verification ceiling of clean builds + code review). Not deployed - backend change, same 3-iteration hold as Stage 14J.

**Files touched**: `backend/Garmetix.Api/Garmetix.Api.csproj` (new package ref), `backend/Garmetix.Api/Assistant/AssistantMcpTools.cs` (new), `backend/Garmetix.Api/Assistant/AssistantOptions.cs`, `backend/Garmetix.Api/Program.cs`, `backend/Garmetix.Api/appsettings.json`, `backend/Garmetix.Api/appsettings.Development.json`, `frontend/modular/deploy/srp-whole-site-deploy.sh`, `frontend/modular/config/version.ts` (`6.0.58`), `frontend/modular/docs/MODULAR_TODO.md`, `.codex/Assistant_MCP_AI_Sense_TODO.md`, `frontend/modular/docs/stage-14j-crm-main-fixes-todo.md`.

---

## 2026-07-07 - Fixed a broken SRP deploy: Nitro flaky-build bug + npm workspace pin bug

**Type**: deploy tooling + workspace config fix. Triggered by Amit reporting the just-deployed GST pages showed "redirecting" or downloaded as a text file instead of rendering.

**What happened**: Investigated Amit's report and found it affected *every* page of *every* modular app on the live SRP site (not GST-specific) - each was a 16-byte `"Redirecting..."` stub served as `application/octet-stream`. Rolled back `/opt/garmetix-srp/current` to the last known-good release first (asked Amit before this manual server write, since it bypasses the normal deploy process), then root-caused the actual bug while the site stayed safe.

**Root cause 1 (the content bug)**: Nitro's static-generation crawler 404s on every route during a cold build; Nitro's own `defaultHandler` (in `nitropack/dist/core/index.mjs`) converts a 404-with-baseURL-mismatch into a redirect response with body `"Redirecting..."`, which the static generator writes to disk as the entire page. This is **flaky, not deterministic**: a from-scratch build (no `.nuxt`, no cache) either crashes (`createRequire` on a malformed `file:///_entry.js`) or silently degrades to this stub; retrying immediately without re-clearing cache reliably produces correct output. Confirmed NOT caused by anything in the GST session's source changes - reproduced identically on `pos`/`hr`/`ai-sense`/`main`, apps never touched.

**Fix 1**: `frontend/modular/deploy/srp-whole-site-deploy.sh`'s `build_app()` now retries each app's build up to 3 times, checking `.output/public/index.html` size (>200 bytes) after each attempt, and refuses to stage a release if every attempt still produces degenerate output. Does NOT re-clear the Nuxt cache between retries (clearing it reproduces the cold-start crash reliably; keeping leftover state from the failed attempt is what makes the retry succeed).

**Root cause 2 (separate, found while restoring `node_modules` after a routine `npm ci` deleted it and then failed to reinstall)**: every app's `package.json` pins internal `@garmetix/shared-*` workspace packages at a stale exact version (`"6.0.0"`) that doesn't match the real current version (e.g. `shared-api` is `6.0.51`). npm 11.13 treats this mismatch as cause to fetch a registry manifest for the (private, unpublished) package name, which 404s and kills the whole install.

**Fix 2**: relaxed all these pins to `"*"` across `apps/{admin,ai-sense,books,crm,hr,main,pos}/package.json` and `packages/{shared-api,shared-auth,shared-ui}/package.json` (7+3 files) - still resolves to the same local workspace links, confirmed via `git diff` this only touched the version-string pins.

**Root cause 3 (orthogonal, worked around not fixed)**: the deploy script's `dotnet publish` step fails specifically when invoked via Git Bash (not WSL) with a doubled drive-letter path (`C:\c\AIArea\...`). Since this GST session made zero backend changes, redeployed with `--skip-api` rather than debugging this further. Flagged in `.claude/todo.md` as a follow-up (only reproduces via Git Bash; WSL path is unaffected but has its own SSH-key-trust gap, see roadmap note).

**Validation**: after the retry-safe redeploy, verified via direct SSH file-content checks (`wc -c`/`file` on the actual deployed files, not just curl status codes) and `node frontend/modular/scripts/srp-public-acceptance.mjs --live --strict` - fully green, including the asset-reference checks that were silent "missing-assets" warnings in the original (broken) deploy's acceptance run.

**Lesson recorded for future deploys**: an HTTP-200 status code is not sufficient evidence a deployed page actually works - this session's first acceptance run showed all-green on status codes while every page was actually broken. Future SRP deploy verification should check real response body content/size, not just status.

**Why this session happened**: Amit reported the exact bug directly from his own browser after the prior GST deployment session ended.

## 2026-07-07 - Books Stage 14G: full legacy GST menu parity + SRP deploy

**Type**: modular frontend feature (major) + deploy. No backend/DB changes.

**What happened**: Amit asked to port the entire legacy "GST" nav group with no feature left behind (14C.5 only closed export/FY-lock, a narrower scope), then deploy to the `.127` SRP test server. Ran three parallel Explore agents against `frontend/legacy/garmetix-web` (the GSTR-1/3B builder page, the CA-share composable, the two missing pages, and modular reuse patterns) before writing a plan, which Amit approved via ExitPlanMode.

**Files added**:
- `frontend/modular/apps/books/composables/useGstReviewContact.ts` - ported from legacy almost verbatim (CA contact + share log, localStorage).
- `frontend/modular/apps/books/pages/accounting-gst-validation.vue` - new page, ports the post-import validation report field-for-field.
- `frontend/modular/apps/admin/pages/gst-final-acceptance.vue` - new page, fills a route that was registered in `routes.ts` with no page file behind it.
- `frontend/modular/scripts/books-stage14g-closure.mjs`, `frontend/modular/docs/stage-14g-books-gst-full-parity.md`.

**Files majorly rewritten**:
- `frontend/modular/apps/books/pages/gst-returns.vue` - 331 lines -> full manual GSTR-1/GSTR-3B builder (dynamic B2B/B2C/HSN/document/nil rows, fixed GSTR-3B sections, Preview, Load From Books, draft Save/Update/Delete/Mark-Filed, ad-hoc JSON/Excel export, audit trail, typed-confirmation accounting-posting bridge, CA review/send modal).
- `frontend/modular/apps/books/pages/gst-reports.vue` - added the CA share modal.

**Bugs found and fixed in this session (via manual browser testing with the preview dev-server tool, not just builds)**:
- `gst-returns.vue`: a `<USelect>` items array had a `{ label: 'No drafts', value: '' }` fallback - Nuxt UI's Select forbids an empty-string item value and threw a 500 the moment the page loaded with zero saved drafts. Fixed by returning an empty array + `placeholder` prop instead. This pattern pre-existed in the file before this session's edits (i.e. was a latent bug in the page since an earlier stage), only surfaced now because this was the first time the page was actually click-tested live.
- `accounting-gst-validation.vue`: default From/To date range was off by one day (`new Date(y,m,1).toISOString()` converts to UTC, shifting backward for IST). Fixed with a `getTimezoneOffset()`-compensated helper, matching the existing `todayIso()` pattern already used elsewhere in Books pages.
- `books-stage14c5-closure.mjs` (a script from the *previous* session): asserted `stage.includes('Stage 14C.5')` exactly and two now-stale UI-copy markers on `gst-returns.vue` ("stay in controlled flows", "no posting action") - both went stale the moment this session's rewrite legitimately expanded that page's scope, and the exact-stage-string assertion would have broken permanently on every future version bump regardless. Removed the brittle checks; kept the checks that are still true (FY-lock markers, required files/scripts).

**Deploy to `.127` (SRP)**: no `sshpass` and no trusted SSH key existed on this machine. With Amit's explicit approval at each step (asked before installing anything or changing auth mode): generated a dedicated `~/.ssh/garmetix_srp` keypair, Amit ran one interactive password-authenticated command himself to add it to the server's `authorized_keys`, then the real deploy ran via `GARMETIX_PREFER_WSL=false npm run modular:deploy:srp` (forcing Git Bash, since that's where the key/SSH config lives - the tooling's default WSL path has a separate filesystem/SSH config). Also fixed a stale `SRP_API_PROJECT` path in the local out-of-repo deploy config (`~/.config/garmetix/srp-deploy.env` had `legacy/backend/...` which doesn't exist in this workspace layout; corrected to `backend/Garmetix.Api/Garmetix.Api.csproj`). Verified via direct LAN `curl` checks and `node frontend/modular/scripts/srp-public-acceptance.mjs --live` - all apps + API health pass on both public (`srp.aadwikafashion.in`) and LAN (`192.168.11.127:8088`).

**Found but explicitly out of scope**: `/crm/customers` is broken on the live SRP site - a pre-existing, unrelated CRM static-build bug (the route's own output file collides with its `customers/new`/`customers/dues-reconciliation` sub-route directories during prerendering). Not caused by anything in this session (CRM was never touched); flagged via `spawn_task` (task_38949783) for a separate session rather than fixed here.

**Validation**: `node frontend/modular/scripts/validate-structure.mjs`, `books-accounting-contract-check.mjs`, `books-stage14g-closure.mjs`, `books-stage14c5-closure.mjs` (regression, post-fix), `books-stage13d-closure.mjs` (regression) all pass; clean `books-web` and `admin-web` production builds; manual interactive browser verification via the dev-server preview tool for all 4 new/changed pages; live SRP acceptance script all-pass.

**Why this session happened**: Amit's direct request - "check [legacy GST] pages and implement in best possible way, without leaving any features," then deploy to `.127` to verify.

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

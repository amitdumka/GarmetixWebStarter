# Assistant MCP And AI Sense TODO

Source reviewed:

- `C:\Users\amitn\Downloads\garmetix-assistant-README.md`
- `C:\Users\amitn\Downloads\garmetix-assistant-patch.tar.gz`

Status: intake complete, patch not applied directly.

## Immediate Security

- Rotate/revoke the Anthropic API key that was pasted in chat before any deployment.
- Do not commit provider keys to git, `appsettings.json`, `.env.example`, docs, shell history, or deployment scripts.
- Use host-side environment variables only, for example:
  - `Assistant__Enabled=true`
  - `Assistant__Provider=Anthropic`
  - `Assistant__AnthropicApiKey=<server secret>`
  - `Assistant__Model=<approved model>`
- Keep assistant disabled by default in source-controlled configuration.

## Patch Review Notes

- The backend assistant folder is useful as a first design draft.
- The patch `Program.cs` is older than current Version6 API wiring and must not be copied over directly.
- The proposed shared shell launcher must be adapted to the current `ModularAppShell.vue`, not overwritten.
- The proposed tool catalog is read-only and uses `WorkspaceScope.ApplyTo(...)`, which matches the required security direction.
- Conversation/message persistence adds new tables. Treat this as a database-affecting change and run backup/deploy gates before live enablement.

## Stage 14F Plan

### 14F.1 Intake And Roadmap

- Document assistant/MCP architecture.
- Mark secret rotation requirement.
- Add a non-mutating readiness check that verifies the assistant lane stays disabled and no Anthropic-style key is present in source.

### 14F.2 Backend Assistant Foundation

- Complete in `6.0.44`: added `backend/Garmetix.Api/Assistant` files after adapting namespaces and service registration to current Version6.
- Complete in `6.0.44`: added options binding with source config placeholders only.
- Complete in `6.0.44`: added `/api/assistant/chat`, `/api/assistant/conversations`, and `/api/assistant/conversations/{id}/messages` behind authorization.
- Decide persistence method:
  - current implementation uses audited startup SQL on first assistant use,
  - do not enable live assistant without deployment note and backup decision.
- Complete in `6.0.44`: tool calls are logged to Message Logs.

### 14F.3 Shared Frontend Assistant Panel

- Complete in `6.0.45`: added `frontend/modular/packages/shared-ui/components/GarmetixAssistantPanel.vue`.
- Complete in `6.0.45`: added a Nuxt UI top-bar launcher and right-side assistant slideover to the shared modular shell.
- Complete in `6.0.45`: added the modular assistant feature flag and runtime config for each modular app.
- Updated in `6.0.46`: set `NUXT_PUBLIC_GARMETIX_ASSISTANT_ENABLED=true` for modular builds so the launcher is visible after login.
- Use the existing shared API/auth clients.
- Keep UI compact, right-side slideover, bottom-right error toasts, and Nuxt UI 4.9 components.
- Show tool-call badges so users know when data was queried.

### 14F.4 AI Sense Tool Catalog

- Complete in `6.0.46`: added dashboard-backed `get_business_snapshot` and `get_today_snapshot` tools.
- Start with read-only tools only:
  - sales summary,
  - store comparison,
  - low stock,
  - outstanding dues,
  - AI Sense sales/purchase/profit/stock/customer/vendor summaries.
- Reuse current `/api/ai-sense/*` read models where possible.
- Every query must apply workspace scope and current user permissions.
- Add a max row/token cap for every tool.

### 14F.5 AI Sense API Path Hotfix

- Complete in `6.0.47`: fixed shared API URL normalization so modular pages can pass `api/...` paths when the configured base URL is already `/api`.
- Complete in `6.0.47`: added regression readiness for the AI Sense dashboard and stock report paths that were failing as `/api/api/...`.

### 14F.6A AI Sense Runtime Path Guard

- Complete in `6.0.49`: added local AI Sense path normalization so stale installed shared packages cannot create `/api/api/...` calls.
- Complete in `6.0.49`: added workspace-link readiness/repair and wired it into SRP deploy builds through the detected npm command.

### 14F.6B AI Sense Deep Route Hosting Fix

- Complete in `6.0.50`: changed SRP Nginx static hosting to serve generated modular route `index.html` files before directory redirects.
- Complete in `6.0.50`: added acceptance probes for AI Sense sales, purchase, daily and profit analysis deep routes.

### 14F.6C AI Sense Runtime Props API Guard

- Complete in `6.0.51`: changed connected analysis props from type-only props to runtime props so production AI Sense pages keep their configured endpoint.
- Complete in `6.0.51`: added an empty endpoint guard in the AI Sense API client so missing page configuration cannot call `/api/`.

### 14F.7 MCP Server Layer (complete 2026-07-08)

- Added the official `ModelContextProtocol.AspNetCore` SDK (0.4.0-preview.1) to `Garmetix.Api.csproj`.
- `backend/Garmetix.Api/Assistant/AssistantMcpTools.cs`: additive `[McpServerToolType]` wrapper exposing the same 6 tools as `AssistantToolCatalog` (`get_store_sales_summary`, `compare_store_performance`, `get_low_stock_items`, `get_outstanding_dues`, `get_business_snapshot`, `get_today_snapshot`). Every method just forwards to `AssistantToolCatalog.ExecuteAsync` - no business logic duplicated, matching the plan.
- `Program.cs`: `AddHttpContextAccessor()` + `AddMcpServer().WithHttpTransport().WithTools<AssistantMcpTools>()`; route mapped at `POST /api/mcp` only when `Assistant:McpEnabled` is true, gated with `.RequireAuthorization()` - same JWT bearer pipeline as every other endpoint, so an MCP caller is bound by the same `WorkspaceScope` tenant scoping and gets no broader network exposure than the rest of the API.
- New separate flag `AssistantOptions.McpEnabled` (default `false`), independent of the in-app chat's `Enabled` flag, since MCP callers (Claude Desktop, claude.ai, etc.) are a different risk surface than the in-app chat panel. Both default off in `appsettings.json`/`appsettings.Development.json`.
- Satisfies "local or private-tunnel only until reviewed": the endpoint is exposed exactly as much as the rest of the Version6 API (private network / SRP host) and defaults to disabled - no additional public surface was opened.
- Verified via `dotnet build backend/Garmetix.Api/Garmetix.Api.csproj` (0 errors); not yet live-tested against a real MCP client (Claude Desktop) or deployed - flip `Assistant__McpEnabled=true` (and a real JWT) to exercise it once ready to test live.

### 14F.8 Live Acceptance (complete 2026-07-08/09)

- Assistant is live on `.127`: `Assistant__Enabled=true` and a real Anthropic API key are set in `/etc/garmetix/srp-api.env` on the SRP host, and the API service has been restarted to pick them up (confirmed `active (running)`, `/api/health` healthy, `/api/assistant/chat` returns 401 unauthenticated - route mapped, auth pipeline intact).
- The frontend launcher (sparkle icon) is now wired up too: added `NUXT_PUBLIC_GARMETIX_ASSISTANT_ENABLED=true` (new `SRP_ASSISTANT_ENABLED` deploy config, default true) to `srp-whole-site-deploy.sh`'s `build_app()` - this had never been set by any deploy to date, so the launcher was baked in as hidden on every prior deploy regardless of backend state. Confirmed `assistantEnabled:true` in the live `index.html` runtime config after redeploy.
- Confirmed no secret appears in this repo's git history - the key was set directly on the remote host via a new `--set-assistant-secret` deploy-script flag (reads `GARMETIX_ASSISTANT_ANTHROPIC_API_KEY` from the environment only, never a CLI arg, never committed).
- **Found and fixed a real production incident along the way**: `--skip-api` deploys left the new release's `api/` folder empty. This worked by accident (a running systemd process keeps its already-loaded binary even after `current` moves) until the `--set-assistant-secret` restart step forced systemd to re-read the now-broken symlink, crash-looping the API (`203/EXEC`) for several minutes. Recovered live with Amit's explicit authorization, then fixed `publish_api()` to pull the currently-live `api/` forward from the remote host on every skip-api deploy so this can't recur.
- API/frontend builds and `npm run validate`/`npm run deploy:preflight` all pass (see the version-6.0.61 changelog entry for the full validator-cleanup detail this surfaced).
- Live MCP client acceptance (Claude Desktop) is still not exercised - `Assistant__McpEnabled` remains off by default; this stage covered the chat assistant only.

## Current Decision

Do not apply the Claude patch directly. Treat it as reference code for Version6 Stage 14F implementation after Books `14C.5` unless the user explicitly asks to prioritize assistant work next.

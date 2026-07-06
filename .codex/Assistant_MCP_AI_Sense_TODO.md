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
- Complete in `6.0.45`: added `NUXT_PUBLIC_GARMETIX_ASSISTANT_ENABLED=false` to the modular env example and runtime config for each modular app.
- Use the existing shared API/auth clients.
- Keep UI compact, right-side slideover, bottom-right error toasts, and Nuxt UI 4.9 components.
- Show tool-call badges so users know when data was queried.

### 14F.4 AI Sense Tool Catalog

- Start with read-only tools only:
  - sales summary,
  - store comparison,
  - low stock,
  - outstanding dues,
  - AI Sense sales/purchase/profit/stock/customer/vendor summaries.
- Reuse current `/api/ai-sense/*` read models where possible.
- Every query must apply workspace scope and current user permissions.
- Add a max row/token cap for every tool.

### 14F.5 MCP Server Layer

- After in-app assistant is stable, expose the same read-only tool catalog through MCP.
- Prefer an additive wrapper around the tool catalog instead of duplicating business logic.
- Keep MCP auth/network exposure local or private-tunnel only until reviewed.

### 14F.6 Live Acceptance

- Validate with assistant disabled.
- Validate with assistant enabled using a rotated key on `.127`.
- Run API build, modular check, AI Sense build and app smoke.
- Confirm no secret appears in git diff or deployment logs.
- Deploy only at the configured checkpoint or on explicit request.

## Current Decision

Do not apply the Claude patch directly. Treat it as reference code for Version6 Stage 14F implementation after Books `14C.5` unless the user explicitly asks to prioritize assistant work next.

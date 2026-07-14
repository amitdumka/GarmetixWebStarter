# Stage 14F.1 - AI Assistant And MCP Intake

Version: 6.0.43

## Scope

This stage reviews the Claude-generated Garmetix Assistant patch and adds the implementation lane to the Version6 roadmap without applying unreviewed code or storing provider secrets.

Reviewed inputs:

- `garmetix-assistant-README.md`
- `garmetix-assistant-patch.tar.gz`

## Findings

- The proposed assistant architecture is aligned with the modular direction: one shared ASP.NET Core API, same auth token, same permission model and same workspace scoping.
- The backend patch introduces `/api/assistant/*`, an Anthropic HTTP client, a read-only tool catalog and conversation/message persistence.
- The proposed frontend adds a shared `GarmetixAssistantPanel` and a top-bar launcher in `ModularAppShell`.
- The patch must not be applied directly because its `Program.cs` is older than current Version6 and would risk losing current endpoint wiring.
- The frontend shell changes must be merged into the current shell rather than replacing it.
- The proposed conversation store creates new tables, so live deployment needs a database backup/change note.

## Security Decision

- The assistant remains disabled by default.
- No provider key is committed.
- The pasted Anthropic key must be rotated before any live test.
- Source control may contain only placeholder configuration names such as `Assistant__AnthropicApiKey`.

## Target Implementation Order

1. Adapt backend assistant options, DTOs and endpoints into current Version6.
2. Add read-only AI Sense/store tools with `WorkspaceScope.ApplyTo(...)`.
3. Persist conversations through an approved database change path.
4. Add shared assistant panel and feature-flagged launcher.
5. Add MCP wrapper around the same tool catalog after the in-app assistant works.
6. Enable on `.127` only with a rotated server-side key and live acceptance checks.

## Validation

- `npm run modular:assistant:mcp-intake`
- `npm --prefix frontend/modular run check`

## Next

Continue Books `14C.5` unless assistant/MCP is explicitly prioritized next.

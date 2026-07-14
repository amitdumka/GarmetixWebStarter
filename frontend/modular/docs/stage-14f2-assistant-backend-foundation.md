# Stage 14F.2 - Assistant Backend Foundation

Version: 6.0.44

## Scope

This stage incorporates the backend Garmetix Assistant foundation from the reviewed Claude patch into the current Version6 API without replacing the current `Program.cs`.

Implemented:

- Added `backend/Garmetix.Api/Assistant`.
- Added `using Garmetix.Api.Assistant;` to the current API startup.
- Registered assistant options and services next to the GSTIN/service setup area.
- Registered `AssistantAnthropicClient` through `HttpClient`.
- Mapped `app.MapAssistantEndpoints()` next to the dashboard endpoint map.
- Added disabled-by-default `Assistant` sections to `appsettings.json` and `appsettings.Development.json`.
- Adapted the tool catalog to the current Version6 sales/store model.

## Safety

- Assistant remains disabled by default.
- `AnthropicApiKey` is intentionally empty in source-controlled config.
- Provider keys must be supplied only through host-side environment variables or a secret manager.
- Conversation tables are created only when the assistant is enabled and used.
- The current tool catalog is read-only.

## Validation

- `dotnet build backend/Garmetix.Api/Garmetix.Api.csproj -c Release`
- `npm run modular:assistant:backend-foundation`
- `npm --prefix frontend/modular run check`

## Next

Add the shared frontend assistant panel and shell launcher behind a feature flag, then extend AI Sense read-only tools and MCP wrapping after the in-app assistant is stable.

# Garmetix Assistant

The Garmetix Assistant is an internal chat feature embedded in the Garmetix SRP
platform. It answers operational questions (sales, inventory, dues,
store/company performance) by calling a fixed, read-only set of tools defined
in `AssistantToolCatalog.cs` - the same tool catalog is also reachable over MCP
(see `AssistantOptions.McpEnabled`).

## Model providers

The assistant's tool-call loop is written entirely against the
`IAssistantModelClient` interface (`AssistantModelClient.cs`), so which LLM
actually answers is a config switch, not a code change. Two providers are
supported:

| Provider | `Assistant:Provider` value | Class | Notes |
|---|---|---|---|
| Anthropic (Claude) | `anthropic` (default) | `AssistantAnthropicClient` | Production quality. Calls `POST /v1/messages` directly over HTTP, using `tool_use`/`tool_result` content blocks. |
| Google Gemini | `gemini` | `AssistantGeminiClient` | Good for testing on Gemini's free Flash tier before switching to Claude for production. Calls `POST /v1beta/models/{model}:generateContent`, using `functionCall`/`functionResponse` parts. |

`AssistantModelClientFactory` resolves the active client from
`Assistant:Provider` at request time - `AssistantChatService` never depends on
a concrete provider client.

`AssistantToolCatalog.cs` is completely provider-agnostic: it only ever sees
a tool name and a JSON input string, and returns a JSON result string,
regardless of which provider is calling it. Each client is responsible for
translating between that neutral shape and its own wire format:

- **Anthropic**: `input_schema` is sent through as Anthropic's native tool
  schema shape; a tool call becomes a `tool_use` content block (with a real
  `id` the model assigned); a tool result is sent back as a `tool_result`
  block matched by that same `tool_use_id`.
- **Gemini**: the same tool schemas are translated into Gemini's
  `functionDeclarations` format (`name`/`description`/`parameters` - the
  `input_schema` JSON Schema body is reused as-is, since Gemini's parameter
  schema is a JSON-Schema-compatible OpenAPI subset and the catalog only uses
  basic `type`/`properties`/`required`/`enum` keywords). Gemini's
  `functionCall` parts have no call id, so `AssistantGeminiClient` synthesizes
  one locally purely to satisfy the shared `AssistantToolCall`/
  `AssistantToolResult` shape; the actual `functionCall`/`functionResponse`
  pairing Gemini relies on is by function **name**, not id.


## Switching providers

Set `Assistant:Provider` to `"anthropic"` or `"gemini"` in `appsettings.json`
(or the `Assistant__Provider` environment variable). Switching to `gemini`
also requires `Gemini:Enabled` to be `true` and a valid `Gemini:ApiKey` -
`AssistantModelClientFactory` throws a clear `InvalidOperationException`
rather than silently falling back if you set the provider without enabling
the Gemini section.

```jsonc
"Assistant": {
  "Enabled": true,
  "Provider": "gemini",      // or "anthropic"
  // ...AnthropicApiKey etc. only matter when Provider is "anthropic"
},
"Gemini": {
  "Enabled": true,
  "ApiKey": "",              // set via Gemini__ApiKey env var / secrets manager, never commit it
  "BaseUrl": "https://generativelanguage.googleapis.com",
  "Model": "gemini-2.5-flash",
  "MaxOutputTokens": 1024,
  "TimeoutSeconds": 60
}
```

### Getting a free Gemini API key

1. Go to [aistudio.google.com](https://aistudio.google.com) and sign in with a
   Google account.
2. Open **Get API key** (`aistudio.google.com/apikey`) and create a key.
3. This is a **Google AI Studio API key** - it is unrelated to a Gemini Pro/
   Advanced *consumer* subscription (the paid chatbot plan). You do not need
   a paid subscription to get a free-tier API key; the free tier is rate- and
   quota-limited but works fine for testing the assistant's tool-call loop.
4. Set the key via `Gemini__ApiKey` (environment variable) or your host's
   secrets manager - never commit it to `appsettings.json`.

## Configuration reference

`AssistantOptions` (section `Assistant`):

| Key | Default | Purpose |
|---|---|---|
| `Enabled` | `false` | Master switch for the whole assistant feature (chat + MCP). |
| `McpEnabled` | `false` | Separately exposes the tool catalog over MCP. |
| `Provider` | `anthropic` | `anthropic` or `gemini` - which `IAssistantModelClient` `AssistantModelClientFactory` resolves. |
| `AnthropicApiKey` | `""` | Only used when `Provider` is `anthropic`. |
| `AnthropicBaseUrl` | `https://api.anthropic.com` | |
| `AnthropicVersion` | `2023-06-01` | |
| `Model` | `claude-sonnet-4-6` | Anthropic model id. |
| `MaxOutputTokens` | `1024` | |
| `MaxToolIterations` | `6` | Safety cap on tool-call round trips per user message, applies regardless of provider. |
| `MaxHistoryMessages` | `20` | |
| `TimeoutSeconds` | `60` | |

`GeminiOptions` (section `Gemini`):

| Key | Default | Purpose |
|---|---|---|
| `Enabled` | `false` | Must be `true` for `Assistant:Provider = "gemini"` to resolve - an independent safety gate. |
| `ApiKey` | `""` | Google AI Studio API key - see above. |
| `BaseUrl` | `https://generativelanguage.googleapis.com` | |
| `Model` | `gemini-2.5-flash` | Free-tier-friendly default. |
| `MaxOutputTokens` | `1024` | |
| `TimeoutSeconds` | `60` | |

## Architecture notes for future providers

Adding a third provider means: implement `IAssistantModelClient.SendAsync`
(translate `AssistantConversationState` + the tool schemas into the
provider's request, translate the response into an `AssistantModelTurn`),
add an options class mirroring `GeminiOptions`, register it in `Program.cs`,
and add one more branch to `AssistantModelClientFactory`. `AssistantChatService`
and `AssistantToolCatalog` need no changes.

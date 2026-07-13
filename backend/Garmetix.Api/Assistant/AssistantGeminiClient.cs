using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace Garmetix.Api.Assistant;

/// <summary>
/// Calls Google Gemini's generateContent endpoint
/// (https://ai.google.dev/api/generate-content) as an alternative to
/// AssistantAnthropicClient - selected via Assistant:Provider = "gemini",
/// e.g. to run the assistant on Gemini's free Flash tier for testing before
/// switching to Claude for production quality. Implements the same
/// IAssistantModelClient interface, translating the provider-neutral
/// AssistantConversationState into Gemini's functionCall/functionResponse
/// "parts" shape and translating the response back.
///
/// Gemini has no equivalent of Anthropic's tool_use.id - a functionCall part
/// only carries a name - so this client synthesizes a per-call id locally
/// (never sent to Gemini) purely so AssistantChatService's loop has
/// something to key AssistantToolResult.ToolCallId on; the actual
/// functionCall/functionResponse pairing Gemini itself relies on is by
/// AssistantToolResult.ToolName instead.
/// </summary>
public sealed class AssistantGeminiClient(HttpClient httpClient, IOptions<GeminiOptions> options) : IAssistantModelClient
{
    public async Task<AssistantModelTurn> SendAsync(
        AssistantConversationState state,
        IReadOnlyList<object> toolSchemas,
        string systemPrompt,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var payload = BuildRequestBody(state, toolSchemas, systemPrompt, settings.MaxOutputTokens);

        var url = $"{settings.BaseUrl.TrimEnd('/')}/v1beta/models/{settings.Model}:generateContent?key={Uri.EscapeDataString(settings.ApiKey)}";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = JsonContent.Create(payload);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var response = await httpClient.SendAsync(request, linkedCts.Token);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new AssistantModelException("Gemini", (int)response.StatusCode, body);
        }

        var node = JsonNode.Parse(body) ?? throw new AssistantModelException("Gemini", 0, "Empty response body from Gemini API.");
        return ParseResponse(node);
    }

    private static JsonObject BuildRequestBody(AssistantConversationState state, IReadOnlyList<object> toolSchemas, string systemPrompt, int maxOutputTokens)
    {
        var contents = new JsonArray();

        foreach (var turn in state.Turns)
        {
            if (turn.Role == AssistantTurnRole.User)
            {
                if (turn.ToolResults.Count > 0)
                {
                    var parts = new JsonArray();
                    foreach (var result in turn.ToolResults)
                    {
                        parts.Add(new JsonObject
                        {
                            ["functionResponse"] = new JsonObject
                            {
                                ["name"] = result.ToolName,
                                ["response"] = ParseOrWrap(result.ResultJson)
                            }
                        });
                    }

                    contents.Add(new JsonObject { ["role"] = "user", ["parts"] = parts });
                }
                else
                {
                    contents.Add(new JsonObject
                    {
                        ["role"] = "user",
                        ["parts"] = new JsonArray { new JsonObject { ["text"] = turn.Text ?? string.Empty } }
                    });
                }

                continue;
            }

            var modelParts = new JsonArray();
            if (!string.IsNullOrEmpty(turn.Text))
            {
                modelParts.Add(new JsonObject { ["text"] = turn.Text });
            }

            foreach (var call in turn.ToolCalls)
            {
                modelParts.Add(new JsonObject
                {
                    ["functionCall"] = new JsonObject
                    {
                        ["name"] = call.Name,
                        ["args"] = ParseOrWrap(call.InputJson)
                    }
                });
            }

            contents.Add(new JsonObject { ["role"] = "model", ["parts"] = modelParts });
        }

        return new JsonObject
        {
            ["system_instruction"] = new JsonObject
            {
                ["parts"] = new JsonArray { new JsonObject { ["text"] = systemPrompt } }
            },
            ["contents"] = contents,
            ["tools"] = new JsonArray
            {
                new JsonObject { ["functionDeclarations"] = BuildFunctionDeclarations(toolSchemas) }
            },
            ["generationConfig"] = new JsonObject { ["maxOutputTokens"] = maxOutputTokens }
        };
    }

    /// <summary>
    /// Translates our existing Anthropic-shaped tool schemas
    /// (name/description/input_schema) into Gemini's functionDeclarations
    /// format (name/description/parameters) - the JSON Schema body itself
    /// is reused as-is since AssistantToolCatalog only uses the basic
    /// type/properties/required/enum keywords Gemini's OpenAPI-subset
    /// schema also supports.
    /// </summary>
    private static JsonArray BuildFunctionDeclarations(IReadOnlyList<object> toolSchemas)
    {
        var declarations = new JsonArray();
        foreach (var tool in toolSchemas)
        {
            var node = JsonSerializer.SerializeToNode(tool)!.AsObject();
            declarations.Add(new JsonObject
            {
                ["name"] = node["name"]?.DeepClone(),
                ["description"] = node["description"]?.DeepClone(),
                ["parameters"] = node["input_schema"]?.DeepClone()
            });
        }

        return declarations;
    }

    private static AssistantModelTurn ParseResponse(JsonNode response)
    {
        var candidate = response["candidates"]?.AsArray().FirstOrDefault();
        if (candidate is null)
        {
            var blockReason = response["promptFeedback"]?["blockReason"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(blockReason))
            {
                throw new AssistantModelException("Gemini", 0, $"Prompt blocked: {blockReason}");
            }

            return new AssistantModelTurn { WantsToolCalls = false, FinalText = string.Empty };
        }

        var parts = candidate["content"]?["parts"]?.AsArray() ?? new JsonArray();

        var toolCalls = new List<AssistantToolCall>();
        var textBuilder = new StringBuilder();
        var index = 0;

        foreach (var part in parts)
        {
            if (part is null)
            {
                continue;
            }

            var functionCall = part["functionCall"];
            if (functionCall is not null)
            {
                var name = functionCall["name"]?.GetValue<string>() ?? string.Empty;
                var inputJson = functionCall["args"]?.ToJsonString() ?? "{}";
                toolCalls.Add(new AssistantToolCall($"gemini-call-{index++}", name, inputJson));
                continue;
            }

            var text = part["text"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(text))
            {
                textBuilder.Append(text);
            }
        }

        if (toolCalls.Count > 0)
        {
            return new AssistantModelTurn { WantsToolCalls = true, ToolCalls = toolCalls };
        }

        return new AssistantModelTurn { WantsToolCalls = false, FinalText = textBuilder.ToString() };
    }

    private static JsonNode? ParseOrWrap(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return JsonValue.Create(json);
        }
    }
}

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace Garmetix.Api.Assistant;

/// <summary>
/// Calls the Anthropic Messages API (https://api.anthropic.com/v1/messages)
/// directly over HttpClient. This is the same "custom tool use" mechanism that
/// underlies MCP - a server-side tool loop, describing tools as JSON schema
/// and executing whichever ones the model asks for. If you later want this
/// same tool set reachable from Claude Desktop / claude.ai as a proper MCP
/// server, wrap AssistantToolCatalog with the ModelContextProtocol NuGet
/// package's [McpServerTool] attributes - the business logic here does not
/// need to change.
///
/// Implements IAssistantModelClient by translating the provider-neutral
/// AssistantConversationState into Anthropic's tool_use/tool_result content
/// block shape and translating the response back - the request/response
/// mechanics below are otherwise unchanged from before this abstraction.
/// </summary>
public sealed class AssistantAnthropicClient(HttpClient httpClient, IOptions<AssistantOptions> options) : IAssistantModelClient
{
    public async Task<AssistantModelTurn> SendAsync(
        AssistantConversationState state,
        IReadOnlyList<object> toolSchemas,
        string systemPrompt,
        CancellationToken cancellationToken)
    {
        var messages = BuildMessages(state);
        var response = await SendRawAsync(messages, toolSchemas, systemPrompt, cancellationToken);
        return ParseResponse(response);
    }

    private async Task<JsonNode> SendRawAsync(JsonArray messages, IReadOnlyList<object> tools, string systemPrompt, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        var payload = new JsonObject
        {
            ["model"] = settings.Model,
            ["max_tokens"] = settings.MaxOutputTokens,
            ["system"] = systemPrompt,
            ["messages"] = messages,
            ["tools"] = JsonSerializer.SerializeToNode(tools)
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.AnthropicBaseUrl.TrimEnd('/')}/v1/messages");
        request.Headers.Add("x-api-key", settings.AnthropicApiKey);
        request.Headers.Add("anthropic-version", settings.AnthropicVersion);
        request.Content = JsonContent.Create(payload);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var response = await httpClient.SendAsync(request, linkedCts.Token);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new AssistantModelException("Anthropic", (int)response.StatusCode, body);
        }

        return JsonNode.Parse(body) ?? throw new AssistantModelException("Anthropic", 0, "Empty response body from Anthropic API.");
    }

    private static JsonArray BuildMessages(AssistantConversationState state)
    {
        var messages = new JsonArray();

        foreach (var turn in state.Turns)
        {
            if (turn.Role == AssistantTurnRole.User)
            {
                if (turn.ToolResults.Count > 0)
                {
                    var content = new JsonArray();
                    foreach (var result in turn.ToolResults)
                    {
                        content.Add(new JsonObject
                        {
                            ["type"] = "tool_result",
                            ["tool_use_id"] = result.ToolCallId,
                            ["content"] = result.ResultJson
                        });
                    }

                    messages.Add(new JsonObject { ["role"] = "user", ["content"] = content });
                }
                else
                {
                    messages.Add(new JsonObject { ["role"] = "user", ["content"] = turn.Text ?? string.Empty });
                }

                continue;
            }

            var assistantContent = new JsonArray();
            if (!string.IsNullOrEmpty(turn.Text))
            {
                assistantContent.Add(new JsonObject { ["type"] = "text", ["text"] = turn.Text });
            }

            foreach (var call in turn.ToolCalls)
            {
                assistantContent.Add(new JsonObject
                {
                    ["type"] = "tool_use",
                    ["id"] = call.Id,
                    ["name"] = call.Name,
                    ["input"] = JsonNode.Parse(string.IsNullOrWhiteSpace(call.InputJson) ? "{}" : call.InputJson)
                });
            }

            messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = assistantContent });
        }

        return messages;
    }

    private static AssistantModelTurn ParseResponse(JsonNode response)
    {
        var stopReason = response["stop_reason"]?.GetValue<string>();
        var contentArray = response["content"]?.AsArray() ?? new JsonArray();

        if (string.Equals(stopReason, "tool_use", StringComparison.Ordinal))
        {
            var toolCalls = new List<AssistantToolCall>();
            foreach (var block in contentArray)
            {
                if (block is null || block["type"]?.GetValue<string>() != "tool_use")
                {
                    continue;
                }

                var id = block["id"]!.GetValue<string>();
                var name = block["name"]!.GetValue<string>();
                var inputJson = block["input"]?.ToJsonString() ?? "{}";
                toolCalls.Add(new AssistantToolCall(id, name, inputJson));
            }

            return new AssistantModelTurn { WantsToolCalls = true, ToolCalls = toolCalls };
        }

        var text = string.Concat(contentArray
            .Where(block => block is not null && block["type"]?.GetValue<string>() == "text")
            .Select(block => block!["text"]!.GetValue<string>()));

        return new AssistantModelTurn { WantsToolCalls = false, FinalText = text };
    }
}

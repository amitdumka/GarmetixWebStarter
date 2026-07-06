using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace Garmetix.Api.Assistant;

/// <summary>
/// Calls the Anthropic Messages API (https://api.anthropic.com/v1/messages)
/// directly over HTTP. This is the same "custom tool use" mechanism that
/// underlies MCP - a server-side tool loop, describing tools as JSON schema
/// and executing whichever ones the model asks for. If you later want this
/// same tool set reachable from Claude Desktop / claude.ai as a proper MCP
/// server, wrap AssistantToolCatalog with the ModelContextProtocol NuGet
/// package's [McpServerTool] attributes - the business logic here does not
/// need to change.
/// </summary>
public sealed class AssistantAnthropicClient(HttpClient httpClient, IOptions<AssistantOptions> options)
{
    public async Task<JsonNode> SendAsync(JsonArray messages, IReadOnlyList<object> tools, string systemPrompt, CancellationToken cancellationToken)
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
            throw new AssistantAnthropicException((int)response.StatusCode, body);
        }

        return JsonNode.Parse(body) ?? throw new AssistantAnthropicException(0, "Empty response body from Anthropic API.");
    }
}

public sealed class AssistantAnthropicException(int statusCode, string body)
    : Exception($"Anthropic API request failed (status {statusCode}): {Truncate(body)}")
{
    public int StatusCode { get; } = statusCode;

    private static string Truncate(string value) => value.Length > 500 ? value[..500] + "..." : value;
}

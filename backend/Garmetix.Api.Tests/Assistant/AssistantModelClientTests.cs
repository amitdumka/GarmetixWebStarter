using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Garmetix.Api.Assistant;
using Microsoft.Extensions.Options;
using Xunit;

namespace Garmetix.Api.Tests.Assistant;

/// <summary>
/// Exercises AssistantAnthropicClient and AssistantGeminiClient end-to-end
/// through IAssistantModelClient.SendAsync against a fake HttpMessageHandler
/// (no real network/credentials involved) - this is the closest available
/// substitute for a live call in an environment with no Anthropic/Gemini API
/// keys configured. Verifies both directions of the translation each client
/// is responsible for: our provider-neutral AssistantConversationState/tool
/// schemas -> the provider's wire request shape, and the provider's wire
/// response shape -> AssistantModelTurn.
/// </summary>
public sealed class AssistantModelClientTests
{
    private static readonly IReadOnlyList<object> ToolSchemas =
    [
        new
        {
            name = "get_low_stock_items",
            description = "List low stock items.",
            input_schema = new
            {
                type = "object",
                properties = new { thresholdQty = new { type = "number" } },
                required = Array.Empty<string>()
            }
        }
    ];

    [Fact]
    public async Task Anthropic_ToolUseResponse_ParsesIntoToolCalls_AndSendsToolUseBlocksBack()
    {
        var handler = new CapturingHandler(request =>
        {
            var responseBody = """
                {
                  "stop_reason": "tool_use",
                  "content": [
                    { "type": "tool_use", "id": "toolu_1", "name": "get_low_stock_items", "input": { "thresholdQty": 3 } }
                  ]
                }
                """;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(responseBody, Encoding.UTF8, "application/json") };
        });

        var client = new AssistantAnthropicClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com") },
            Options.Create(new AssistantOptions { AnthropicApiKey = "test-key", Model = "claude-sonnet-4-6", MaxOutputTokens = 512, TimeoutSeconds = 30 }));

        var state = new AssistantConversationState();
        state.AddUserText("what is low on stock?");

        var turn = await client.SendAsync(state, ToolSchemas, "system prompt", CancellationToken.None);

        Assert.True(turn.WantsToolCalls);
        var call = Assert.Single(turn.ToolCalls);
        Assert.Equal("toolu_1", call.Id);
        Assert.Equal("get_low_stock_items", call.Name);
        Assert.Equal(3, JsonNode.Parse(call.InputJson)!["thresholdQty"]!.GetValue<int>());

        var sentBody = JsonNode.Parse(handler.LastRequestBody!)!;
        Assert.Equal("claude-sonnet-4-6", sentBody["model"]!.GetValue<string>());
        Assert.Equal("what is low on stock?", sentBody["messages"]![0]!["content"]!.GetValue<string>());

        // Feed the tool result back and confirm the follow-up request carries a tool_result block.
        state.AddAssistantTurn(turn.FinalText, turn.ToolCalls);
        state.AddToolResults([new AssistantToolResult(call.Id, call.Name, """{"items":[]}""", false)]);

        var followUpBody = """{ "stop_reason": "end_turn", "content": [ { "type": "text", "text": "Nothing is low on stock." } ] }""";
        handler.NextResponse = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(followUpBody, Encoding.UTF8, "application/json") };

        var finalTurn = await client.SendAsync(state, ToolSchemas, "system prompt", CancellationToken.None);

        Assert.False(finalTurn.WantsToolCalls);
        Assert.Equal("Nothing is low on stock.", finalTurn.FinalText);

        var followUpSent = JsonNode.Parse(handler.LastRequestBody!)!;
        var assistantMessage = followUpSent["messages"]![1]!;
        Assert.Equal("assistant", assistantMessage["role"]!.GetValue<string>());
        Assert.Equal("tool_use", assistantMessage["content"]![0]!["type"]!.GetValue<string>());

        var toolResultMessage = followUpSent["messages"]![2]!;
        Assert.Equal("user", toolResultMessage["role"]!.GetValue<string>());
        Assert.Equal("tool_result", toolResultMessage["content"]![0]!["type"]!.GetValue<string>());
        Assert.Equal("toolu_1", toolResultMessage["content"]![0]!["tool_use_id"]!.GetValue<string>());
    }

    [Fact]
    public async Task Anthropic_TextResponse_ParsesIntoFinalText()
    {
        var handler = new CapturingHandler(_ =>
        {
            const string body = """{ "stop_reason": "end_turn", "content": [ { "type": "text", "text": "Sales were flat." } ] }""";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        });

        var client = new AssistantAnthropicClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com") },
            Options.Create(new AssistantOptions { AnthropicApiKey = "test-key" }));

        var state = new AssistantConversationState();
        state.AddUserText("how were sales?");

        var turn = await client.SendAsync(state, ToolSchemas, "system prompt", CancellationToken.None);

        Assert.False(turn.WantsToolCalls);
        Assert.Equal("Sales were flat.", turn.FinalText);
    }

    [Fact]
    public async Task Anthropic_ErrorResponse_ThrowsAssistantModelException()
    {
        var handler = new CapturingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("""{"error":"bad key"}""") });

        var client = new AssistantAnthropicClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com") },
            Options.Create(new AssistantOptions { AnthropicApiKey = "test-key" }));

        var state = new AssistantConversationState();
        state.AddUserText("hi");

        var ex = await Assert.ThrowsAsync<AssistantModelException>(
            () => client.SendAsync(state, ToolSchemas, "system prompt", CancellationToken.None));

        Assert.Equal("Anthropic", ex.ProviderName);
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task Gemini_FunctionCallResponse_ParsesIntoToolCalls_AndSendsFunctionResponseBack()
    {
        var handler = new CapturingHandler(_ =>
        {
            const string body = """
                {
                  "candidates": [
                    { "content": { "role": "model", "parts": [ { "functionCall": { "name": "get_low_stock_items", "args": { "thresholdQty": 5 } } } ] } }
                  ]
                }
                """;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        });

        var client = new AssistantGeminiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://generativelanguage.googleapis.com") },
            Options.Create(new GeminiOptions { Enabled = true, ApiKey = "test-gemini-key", Model = "gemini-2.5-flash", MaxOutputTokens = 512, TimeoutSeconds = 30 }));

        var state = new AssistantConversationState();
        state.AddUserText("what is low on stock?");

        var turn = await client.SendAsync(state, ToolSchemas, "system prompt", CancellationToken.None);

        Assert.True(turn.WantsToolCalls);
        var call = Assert.Single(turn.ToolCalls);
        Assert.Equal("get_low_stock_items", call.Name);
        Assert.Equal(5, JsonNode.Parse(call.InputJson)!["thresholdQty"]!.GetValue<int>());
        Assert.Contains("key=test-gemini-key", handler.LastRequestUri);
        Assert.Contains("gemini-2.5-flash", handler.LastRequestUri);

        var sentBody = JsonNode.Parse(handler.LastRequestBody!)!;
        var declarations = sentBody["tools"]![0]!["functionDeclarations"]!.AsArray();
        Assert.Equal("get_low_stock_items", declarations[0]!["name"]!.GetValue<string>());
        Assert.NotNull(declarations[0]!["parameters"]);

        // Feed the tool result back and confirm the follow-up request carries a functionResponse part.
        state.AddAssistantTurn(turn.FinalText, turn.ToolCalls);
        state.AddToolResults([new AssistantToolResult(call.Id, call.Name, """{"items":[]}""", false)]);

        const string followUpBody = """
            {
              "candidates": [
                { "content": { "role": "model", "parts": [ { "text": "Nothing is low on stock." } ] } }
              ]
            }
            """;
        handler.NextResponse = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(followUpBody, Encoding.UTF8, "application/json") };

        var finalTurn = await client.SendAsync(state, ToolSchemas, "system prompt", CancellationToken.None);

        Assert.False(finalTurn.WantsToolCalls);
        Assert.Equal("Nothing is low on stock.", finalTurn.FinalText);

        var followUpSent = JsonNode.Parse(handler.LastRequestBody!)!;
        var modelContent = followUpSent["contents"]![1]!;
        Assert.Equal("model", modelContent["role"]!.GetValue<string>());
        Assert.NotNull(modelContent["parts"]![0]!["functionCall"]);

        var toolResultContent = followUpSent["contents"]![2]!;
        Assert.Equal("user", toolResultContent["role"]!.GetValue<string>());
        Assert.Equal("get_low_stock_items", toolResultContent["parts"]![0]!["functionResponse"]!["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task Gemini_TextResponse_ParsesIntoFinalText()
    {
        var handler = new CapturingHandler(_ =>
        {
            const string body = """
                { "candidates": [ { "content": { "role": "model", "parts": [ { "text": "Sales were flat." } ] } } ] }
                """;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        });

        var client = new AssistantGeminiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://generativelanguage.googleapis.com") },
            Options.Create(new GeminiOptions { Enabled = true, ApiKey = "test-gemini-key" }));

        var state = new AssistantConversationState();
        state.AddUserText("how were sales?");

        var turn = await client.SendAsync(state, ToolSchemas, "system prompt", CancellationToken.None);

        Assert.False(turn.WantsToolCalls);
        Assert.Equal("Sales were flat.", turn.FinalText);
    }

    [Fact]
    public async Task Gemini_ErrorResponse_ThrowsAssistantModelException()
    {
        var handler = new CapturingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("""{"error":{"message":"invalid api key"}}""") });

        var client = new AssistantGeminiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://generativelanguage.googleapis.com") },
            Options.Create(new GeminiOptions { Enabled = true, ApiKey = "bad-key" }));

        var state = new AssistantConversationState();
        state.AddUserText("hi");

        var ex = await Assert.ThrowsAsync<AssistantModelException>(
            () => client.SendAsync(state, ToolSchemas, "system prompt", CancellationToken.None));

        Assert.Equal("Gemini", ex.ProviderName);
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public void Factory_GeminiProviderWithoutGeminiEnabled_Throws()
    {
        var factory = new AssistantModelClientFactory(
            new AssistantAnthropicClient(new HttpClient(new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))), Options.Create(new AssistantOptions())),
            new AssistantGeminiClient(new HttpClient(new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))), Options.Create(new GeminiOptions())),
            Options.Create(new GeminiOptions { Enabled = false }),
            Options.Create(new AssistantOptions { Provider = "gemini" }));

        Assert.Throws<InvalidOperationException>(() => factory.GetClient());
    }

    [Fact]
    public void Factory_GeminiProviderWithGeminiEnabled_ResolvesGeminiClient()
    {
        var geminiClient = new AssistantGeminiClient(new HttpClient(new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))), Options.Create(new GeminiOptions()));
        var factory = new AssistantModelClientFactory(
            new AssistantAnthropicClient(new HttpClient(new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))), Options.Create(new AssistantOptions())),
            geminiClient,
            Options.Create(new GeminiOptions { Enabled = true }),
            Options.Create(new AssistantOptions { Provider = "gemini" }));

        Assert.Same(geminiClient, factory.GetClient());
    }

    [Fact]
    public void Factory_DefaultProvider_ResolvesAnthropicClient()
    {
        var anthropicClient = new AssistantAnthropicClient(new HttpClient(new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))), Options.Create(new AssistantOptions()));
        var factory = new AssistantModelClientFactory(
            anthropicClient,
            new AssistantGeminiClient(new HttpClient(new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))), Options.Create(new GeminiOptions())),
            Options.Create(new GeminiOptions()),
            Options.Create(new AssistantOptions { Provider = "anthropic" }));

        Assert.Same(anthropicClient, factory.GetClient());
    }

    private sealed class CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public string? LastRequestBody { get; private set; }
        public string? LastRequestUri { get; private set; }
        public Func<HttpRequestMessage, HttpResponseMessage>? NextResponse { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.ToString();
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            var responder = NextResponse ?? respond;
            return responder(request);
        }
    }
}

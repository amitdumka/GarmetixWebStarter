namespace Garmetix.Api.Assistant;

/// <summary>
/// Provider-agnostic abstraction over "an LLM that can call tools" - the
/// Garmetix Assistant tool-call loop (AssistantChatService) is written
/// entirely against this interface so it works unchanged whether the
/// configured provider is Anthropic (AssistantAnthropicClient) or
/// Google Gemini (AssistantGeminiClient). Each implementation is
/// responsible for translating AssistantConversationState/tool schemas
/// into its own wire format and translating its response back into an
/// AssistantModelTurn - none of that provider-specific shape ever leaks
/// into the caller.
/// </summary>
public interface IAssistantModelClient
{
    Task<AssistantModelTurn> SendAsync(
        AssistantConversationState state,
        IReadOnlyList<object> toolSchemas,
        string systemPrompt,
        CancellationToken cancellationToken);
}

/// <summary>
/// Resolves the active IAssistantModelClient for the configured
/// Assistant:Provider ("anthropic" or "gemini"), so AssistantChatService
/// never has to depend on a concrete provider client directly.
/// </summary>
public interface IAssistantModelClientFactory
{
    IAssistantModelClient GetClient();
}

public enum AssistantTurnRole
{
    User,
    Assistant
}

/// <summary>
/// A single requested tool invocation from the model - "call get_low_stock_items
/// with this JSON input". Id is only meaningful to the provider that produced it
/// (Anthropic's tool_use.id; Gemini has no equivalent so a synthetic id is used)
/// and is echoed back verbatim in the matching AssistantToolResult so that
/// provider can re-attach its result to the right call when rebuilding its
/// own wire format on the next turn.
/// </summary>
public sealed record AssistantToolCall(string Id, string Name, string InputJson);

/// <summary>
/// The executed result of one AssistantToolCall, carrying both the
/// originating call's Id (Anthropic matches tool_result.tool_use_id by this)
/// and its ToolName (Gemini matches functionResponse.name by this instead -
/// it has no call-id concept), so either provider can rebuild its own
/// tool-result wire shape from the same data.
/// </summary>
public sealed record AssistantToolResult(string ToolCallId, string ToolName, string ResultJson, bool IsError);

/// <summary>
/// One turn of the conversation, in a shape neutral enough for either
/// provider to translate to/from its own message format. A user turn is
/// either plain text (the human's message) or a set of tool results being
/// fed back after the assistant asked for them; an assistant turn is
/// optional text plus zero or more tool calls it asked for.
/// </summary>
public sealed class AssistantConversationTurn
{
    public required AssistantTurnRole Role { get; init; }
    public string? Text { get; init; }
    public IReadOnlyList<AssistantToolCall> ToolCalls { get; init; } = [];
    public IReadOnlyList<AssistantToolResult> ToolResults { get; init; } = [];
}

/// <summary>
/// The full conversation passed to IAssistantModelClient.SendAsync, built
/// once per chat request from prior history + the new user message, then
/// mutated in place across tool-loop iterations by AssistantChatService.
/// </summary>
public sealed class AssistantConversationState
{
    public List<AssistantConversationTurn> Turns { get; } = [];

    public void AddUserText(string text) =>
        Turns.Add(new AssistantConversationTurn { Role = AssistantTurnRole.User, Text = text });

    public void AddAssistantTurn(string? text, IReadOnlyList<AssistantToolCall> toolCalls) =>
        Turns.Add(new AssistantConversationTurn { Role = AssistantTurnRole.Assistant, Text = text, ToolCalls = toolCalls });

    public void AddToolResults(IReadOnlyList<AssistantToolResult> results) =>
        Turns.Add(new AssistantConversationTurn { Role = AssistantTurnRole.User, ToolResults = results });
}

/// <summary>
/// What the model did on one turn: either it wants to call tools (ToolCalls
/// populated), or it produced a final text reply (FinalText populated).
/// </summary>
public sealed class AssistantModelTurn
{
    public required bool WantsToolCalls { get; init; }
    public IReadOnlyList<AssistantToolCall> ToolCalls { get; init; } = [];
    public string? FinalText { get; init; }
}

/// <summary>
/// A failed call to any configured model provider's API. ProviderName
/// distinguishes which one failed (Anthropic vs Gemini) for logging/error
/// messages without needing a provider-specific exception type per client.
/// </summary>
public sealed class AssistantModelException(string providerName, int statusCode, string body)
    : Exception($"{providerName} API request failed (status {statusCode}): {AssistantModelException.Truncate(body)}")
{
    public string ProviderName { get; } = providerName;
    public int StatusCode { get; } = statusCode;

    private static string Truncate(string value) => value.Length > 500 ? value[..500] + "..." : value;
}

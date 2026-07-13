namespace Garmetix.Api.Assistant;

/// <summary>
/// Configuration for the embedded "Garmetix Assistant" chat feature.
/// Bound from the "Assistant" section in appsettings.json / environment variables.
/// Keep AnthropicApiKey out of source control - set it via environment variable
/// Assistant__AnthropicApiKey or a secrets manager on the host.
/// </summary>
public sealed class AssistantOptions
{
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Separate gate for exposing the same read-only tool catalog over MCP
    /// (Model Context Protocol) instead of / in addition to the in-app chat.
    /// Off by default - MCP callers are external tool clients (Claude Desktop,
    /// claude.ai, etc.), a different risk surface than the in-app chat panel,
    /// so this must be opted into explicitly even if Enabled is already true.
    /// </summary>
    public bool McpEnabled { get; set; } = false;

    /// <summary>
    /// Which model provider backs the assistant's tool-call loop: "anthropic"
    /// (default, production quality) or "gemini" (Google's free Flash tier,
    /// good for testing without spending Anthropic credits - see GeminiOptions).
    /// Resolved by AssistantModelClientFactory.
    /// </summary>
    public string Provider { get; set; } = "anthropic";

    public string AnthropicApiKey { get; set; } = string.Empty;

    public string AnthropicBaseUrl { get; set; } = "https://api.anthropic.com";

    public string AnthropicVersion { get; set; } = "2023-06-01";

    public string Model { get; set; } = "claude-sonnet-4-6";

    public int MaxOutputTokens { get; set; } = 1024;

    /// <summary>
    /// Safety cap on how many tool-call round-trips the assistant can make
    /// while answering a single user message, to avoid runaway loops.
    /// </summary>
    public int MaxToolIterations { get; set; } = 6;

    /// <summary>
    /// How many prior messages from the conversation are replayed back to
    /// the model as context on each turn.
    /// </summary>
    public int MaxHistoryMessages { get; set; } = 20;

    public int TimeoutSeconds { get; set; } = 60;
}

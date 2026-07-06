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

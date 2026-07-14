namespace Garmetix.Api.Assistant;

/// <summary>
/// Configuration for using Google Gemini as the Garmetix Assistant's model
/// provider instead of Anthropic - see AssistantOptions.Provider. Bound from
/// the "Gemini" section in appsettings.json / environment variables. Get a
/// free API key at https://aistudio.google.com/apikey (a Google AI Studio
/// key, not a Gemini Pro consumer subscription - the two are unrelated).
/// Keep ApiKey out of source control - set it via environment variable
/// Gemini__ApiKey or a secrets manager on the host.
/// </summary>
public sealed class GeminiOptions
{
    /// <summary>
    /// Independent safety gate from AssistantOptions.Provider - if Provider
    /// is set to "gemini" but this is left false, AssistantModelClientFactory
    /// refuses to resolve the Gemini client rather than silently calling out
    /// with whatever ApiKey happens to be configured.
    /// </summary>
    public bool Enabled { get; set; } = false;

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";

    public string Model { get; set; } = "gemini-2.5-flash";

    public int MaxOutputTokens { get; set; } = 1024;

    public int TimeoutSeconds { get; set; } = 60;
}

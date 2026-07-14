using Microsoft.Extensions.Options;

namespace Garmetix.Api.Assistant;

/// <summary>
/// Resolves the IAssistantModelClient to use for AssistantOptions.Provider
/// ("anthropic" or "gemini"), so AssistantChatService depends only on
/// IAssistantModelClient/IAssistantModelClientFactory and never on a
/// concrete provider client.
/// </summary>
public sealed class AssistantModelClientFactory(
    AssistantAnthropicClient anthropicClient,
    AssistantGeminiClient geminiClient,
    IOptions<GeminiOptions> geminiOptions,
    IOptions<AssistantOptions> assistantOptions) : IAssistantModelClientFactory
{
    public IAssistantModelClient GetClient()
    {
        var provider = assistantOptions.Value.Provider;

        if (string.Equals(provider, "gemini", StringComparison.OrdinalIgnoreCase))
        {
            if (!geminiOptions.Value.Enabled)
            {
                throw new InvalidOperationException(
                    "Assistant:Provider is 'gemini' but Gemini:Enabled is false. Set Gemini:Enabled to true and provide Gemini:ApiKey.");
            }

            return geminiClient;
        }

        return anthropicClient;
    }
}

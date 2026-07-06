using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Garmetix.Api.Messages;
using Garmetix.Api.Workspace;
using Microsoft.Extensions.Options;

namespace Garmetix.Api.Assistant;

public sealed class AssistantChatService(
    AssistantAnthropicClient anthropicClient,
    AssistantToolCatalog toolCatalog,
    AssistantConversationStore conversationStore,
    ApplicationMessageLogService messageLog,
    IOptions<AssistantOptions> options)
{
    private const string SystemPromptTemplate = """
        You are the Garmetix Assistant, an internal tool embedded in the Garmetix
        SRP platform for a garment retail store chain with multiple companies,
        store groups and stores. You help store managers, company admins and ops
        staff query sales, inventory and dues data.

        Rules:
        - Only discuss Garmetix SRP operational data (sales, inventory, dues,
          store/company performance). Politely decline unrelated requests.
        - Always call a tool to fetch live data before answering with numbers -
          never guess or estimate figures.
        - Present comparisons as a short table. Always state the date range you
          are reporting on.
        - If a tool call fails or returns no rows, say so plainly rather than
          inventing a plausible-sounding answer.
        - You only have access to data the current user is authorized to see
          (scope: {0}). If asked about something outside that scope, say so.
        - The current app screen is: {1}.
        """;

    public async Task<AssistantChatResponse> HandleAsync(AssistantChatRequest request, HttpContext context, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            throw new InvalidOperationException("The Garmetix Assistant is not enabled on this environment.");
        }

        var user = context.User;
        var userId = ReadGuidClaim(user, ClaimTypes.NameIdentifier);
        var userName = user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue("name");
        var companyId = ReadGuidClaim(user, "companyId");
        var storeGroupId = ReadGuidClaim(user, "storeGroupId");
        var storeId = ReadGuidClaim(user, "storeId");

        var conversationId = await conversationStore.GetOrCreateConversationAsync(
            request.ConversationId, userId ?? Guid.Empty, request.AppId, companyId, storeGroupId, storeId, cancellationToken);

        var history = await conversationStore.GetRecentMessagesAsync(conversationId, userId ?? Guid.Empty, settings.MaxHistoryMessages, cancellationToken);

        var messages = new JsonArray();
        foreach (var turn in history)
        {
            messages.Add(new JsonObject { ["role"] = turn.Role, ["content"] = turn.Content });
        }
        messages.Add(new JsonObject { ["role"] = "user", ["content"] = request.Message });

        await conversationStore.AppendMessageAsync(conversationId, "user", request.Message, null, cancellationToken);

        var scopeDescription = DescribeScope(user);
        var systemPrompt = string.Format(SystemPromptTemplate, scopeDescription, request.AppId ?? "unknown");

        var toolCalls = new List<AssistantToolCallDto>();
        string finalReply = "I couldn't complete that request.";

        for (var iteration = 0; iteration < settings.MaxToolIterations; iteration++)
        {
            JsonNode response;
            try
            {
                response = await anthropicClient.SendAsync(messages, toolCatalog.ToolSchemas, systemPrompt, cancellationToken);
            }
            catch (AssistantAnthropicException ex)
            {
                await messageLog.ErrorAsync("Assistant", "ChatError", ex.Message,
                    companyId: companyId, storeGroupId: storeGroupId, storeId: storeId, userId: userId, userName: userName,
                    cancellationToken: cancellationToken);
                throw;
            }

            var stopReason = response["stop_reason"]?.GetValue<string>();
            var contentArray = response["content"]?.AsArray() ?? new JsonArray();

            messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = contentArray.DeepClone() });

            if (!string.Equals(stopReason, "tool_use", StringComparison.Ordinal))
            {
                finalReply = string.Concat(contentArray
                    .Where(block => block is not null && block["type"]?.GetValue<string>() == "text")
                    .Select(block => block!["text"]!.GetValue<string>()));
                break;
            }

            var toolResults = new JsonArray();
            foreach (var block in contentArray)
            {
                if (block is null || block["type"]?.GetValue<string>() != "tool_use")
                {
                    continue;
                }

                var toolUseId = block["id"]!.GetValue<string>();
                var toolName = block["name"]!.GetValue<string>();
                var inputJson = block["input"]?.ToJsonString() ?? "{}";

                string resultJson;
                var success = true;
                try
                {
                    resultJson = await toolCatalog.ExecuteAsync(toolName, inputJson, context, cancellationToken);
                }
                catch (Exception ex)
                {
                    success = false;
                    resultJson = JsonSerializer.Serialize(new { error = ex.Message });
                }

                toolCalls.Add(new AssistantToolCallDto(toolName, inputJson, Truncate(resultJson, 300), success));

                await messageLog.SuccessAsync("Assistant", "ToolCall", $"Assistant executed {toolName}",
                    details: new { input = SafeParse(inputJson), resultPreview = Truncate(resultJson, 500), success },
                    companyId: companyId, storeGroupId: storeGroupId, storeId: storeId, userId: userId, userName: userName,
                    cancellationToken: cancellationToken);

                toolResults.Add(new JsonObject
                {
                    ["type"] = "tool_result",
                    ["tool_use_id"] = toolUseId,
                    ["content"] = resultJson
                });
            }

            messages.Add(new JsonObject { ["role"] = "user", ["content"] = toolResults });
        }

        var toolCallsJson = toolCalls.Count == 0 ? null : JsonSerializer.Serialize(toolCalls);
        await conversationStore.AppendMessageAsync(conversationId, "assistant", finalReply, toolCallsJson, cancellationToken);

        return new AssistantChatResponse(conversationId, finalReply, toolCalls);
    }

    private static string DescribeScope(ClaimsPrincipal user)
    {
        if (WorkspaceScope.HasFullAccess(user)) return "full access to all companies, store groups and stores";

        var storeId = ReadGuidClaim(user, "storeId");
        var storeGroupId = ReadGuidClaim(user, "storeGroupId");
        if (storeId.HasValue) return "a single store only";
        if (storeGroupId.HasValue) return "a single store group only";
        return "the current company only";
    }

    private static Guid? ReadGuidClaim(ClaimsPrincipal user, string claimType)
        => Guid.TryParse(user.FindFirstValue(claimType), out var value) ? value : null;

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max] + "...";

    private static JsonNode? SafeParse(string json)
    {
        try { return JsonNode.Parse(json); }
        catch { return JsonValue.Create(json); }
    }
}

using System.Security.Claims;

namespace Garmetix.Api.Assistant;

public static class AssistantEndpoints
{
    public static RouteGroupBuilder MapAssistantEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/assistant")
            .WithTags("Assistant")
            .RequireAuthorization();

        group.MapPost("/chat", ChatAsync).WithName("PostAssistantChat");
        group.MapGet("/conversations", ConversationsAsync).WithName("GetAssistantConversations");
        group.MapGet("/conversations/{conversationId:guid}/messages", ConversationMessagesAsync).WithName("GetAssistantConversationMessages");

        return group;
    }

    private static async Task<IResult> ChatAsync(
        AssistantChatRequest request,
        AssistantChatService chatService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return Results.BadRequest(new { error = "Message is required." });
        }

        try
        {
            var response = await chatService.HandleAsync(request, context, cancellationToken);
            return Results.Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            // Assistant disabled on this environment, or similar config issue.
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (AssistantAnthropicException ex)
        {
            return Results.Problem("The assistant is temporarily unavailable. Please try again shortly.", statusCode: StatusCodes.Status502BadGateway, title: ex.Message);
        }
    }

    private static async Task<IResult> ConversationsAsync(
        AssistantConversationStore store,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var userId = Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
        var conversations = await store.ListConversationsAsync(userId, 30, cancellationToken);
        return Results.Ok(conversations);
    }

    private static async Task<IResult> ConversationMessagesAsync(
        Guid conversationId,
        AssistantConversationStore store,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var userId = Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
        var messages = await store.GetRecentMessagesAsync(conversationId, userId, 100, cancellationToken);
        return Results.Ok(messages);
    }
}

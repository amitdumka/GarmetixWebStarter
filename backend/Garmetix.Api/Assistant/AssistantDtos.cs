namespace Garmetix.Api.Assistant;

public sealed record AssistantChatRequest(
    string Message,
    Guid? ConversationId,
    string? AppId);

public sealed record AssistantToolCallDto(
    string ToolName,
    string InputJson,
    string ResultSummary,
    bool Success);

public sealed record AssistantChatResponse(
    Guid ConversationId,
    string Reply,
    IReadOnlyList<AssistantToolCallDto> ToolCalls);

public sealed record AssistantConversationSummaryDto(
    Guid Id,
    string? AppId,
    DateTime CreatedAtUtc,
    DateTime LastMessageAtUtc,
    string? LastMessagePreview);

public sealed record AssistantMessageDto(
    Guid Id,
    string Role,
    string Content,
    DateTime CreatedAtUtc);

public sealed record AssistantScopeContextDto(
    bool HasFullAccess,
    Guid? CompanyId,
    string? CompanyName,
    Guid? StoreGroupId,
    string? StoreGroupName,
    Guid? StoreId,
    string? StoreName);

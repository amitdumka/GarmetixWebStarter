namespace Garmetix.Api.Messages;

public sealed record ApplicationMessageLogDto(
    Guid Id,
    DateTime CreatedAtUtc,
    string Level,
    string Source,
    string EventName,
    string Message,
    string? DetailsJson,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    Guid? UserId,
    string? UserName,
    string? Resource,
    Guid OperationId,
    bool Success);

public sealed record ApplicationMessageLogQuery(
    string? Level,
    string? Source,
    string? Search,
    DateTime? FromUtc,
    DateTime? ToUtc,
    Guid? CompanyId,
    Guid? StoreId,
    bool? Success,
    int Take = 100);

public sealed record ApplicationMessageLogCreateRequest(
    string Level,
    string Source,
    string EventName,
    string Message,
    string? DetailsJson = null,
    Guid? CompanyId = null,
    Guid? StoreGroupId = null,
    Guid? StoreId = null,
    Guid? UserId = null,
    string? UserName = null,
    string? Resource = null,
    Guid? OperationId = null,
    bool Success = true);

public sealed record ClientApplicationMessageLogRequest(
    string Level,
    string EventName,
    string Message,
    string? DetailsJson = null,
    string? Resource = null,
    bool Success = true);

public sealed record ApplicationMessageLogOptionsDto(
    IReadOnlyList<string> Levels,
    IReadOnlyList<string> Sources,
    IReadOnlyList<string> Events);

public sealed record ApplicationNotificationDto(
    Guid Id,
    DateTime CreatedAtUtc,
    string Severity,
    string Title,
    string Message,
    string ActionPath);

public sealed record ApplicationNotificationSummaryDto(
    int AttentionCount,
    IReadOnlyList<ApplicationNotificationDto> Items);

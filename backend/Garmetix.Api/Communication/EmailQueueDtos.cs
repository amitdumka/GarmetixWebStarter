namespace Garmetix.Api.Communication;

public sealed record EmailQueueItemSummaryDto(
    Guid Id,
    string Status,
    string Subject,
    string? SourceModule,
    string? SourceType,
    Guid? SourceId,
    string CorrelationId,
    string? ProviderMessageId,
    int AttemptCount,
    DateTime? NextAttemptAtUtc,
    string? LastErrorCode,
    DateTime CreatedAt,
    string? RecipientEmail);

public sealed record EmailDeliveryAttemptDto(int AttemptNumber, DateTime StartedAtUtc, bool WasSuccess, string? ErrorCode, string? ErrorMessage);

public sealed record EmailDeliveryEventDto(string EventType, DateTime OccurredAtUtc);

public sealed record EmailQueueItemDetailDto(
    Guid Id,
    string Status,
    string Subject,
    string HtmlBody,
    string? SourceModule,
    string? SourceType,
    Guid? SourceId,
    string CorrelationId,
    string IdempotencyKey,
    string? ProviderMessageId,
    int AttemptCount,
    int MaxAttempts,
    DateTime? NextAttemptAtUtc,
    string? LastErrorCode,
    string? LastErrorMessage,
    DateTime CreatedAt,
    IReadOnlyList<string> RecipientEmails,
    IReadOnlyList<EmailDeliveryAttemptDto> Attempts,
    IReadOnlyList<EmailDeliveryEventDto> Events);

public sealed record EmailQueueRescheduleRequest(DateTime ScheduledForUtc);

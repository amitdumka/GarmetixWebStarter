namespace Garmetix.Api.FinalAccounts;

public sealed record FinalAccountsBackfillRequest(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    DateTime? From,
    DateTime? To,
    IReadOnlyList<string>? Modules,
    bool? StopOnError,
    string? IdempotencyKey);

public sealed record FinalAccountsSyncOptionsResponse(
    bool ExistingOutboxDetected,
    bool ScheduledModeEnabled,
    string PostingMode,
    int MaxAttempts,
    int RetryDelaySeconds,
    string SafetyMessage,
    IReadOnlyList<string> SupportedModules);

public sealed record FinalAccountsBackfillModulePreviewDto(
    string Module,
    int SourceCount,
    int AlreadyLinkedCount,
    int PendingCount,
    int DriftCount,
    decimal SourceTotal,
    decimal LinkedJournalDebit,
    decimal LinkedJournalCredit);

public sealed record FinalAccountsBackfillPreviewResponse(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    DateTime? From,
    DateTime? To,
    IReadOnlyList<string> Modules,
    int SourceCount,
    int AlreadyLinkedCount,
    int PendingCount,
    int DriftCount,
    decimal SourceTotal,
    bool DryRun,
    bool WritesSourceData,
    string ResumeCheckpoint,
    IReadOnlyList<FinalAccountsBackfillModulePreviewDto> ModulePreviews);

public sealed record FinalAccountsSyncJobItemDto(
    Guid Id,
    string SourceType,
    Guid SourceId,
    string? SourceReference,
    DateTime? SourceDate,
    decimal SourceAmount,
    string SourceHash,
    string Status,
    int AttemptCount,
    DateTime? NextAttemptAt,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record FinalAccountsSyncJobDto(
    Guid Id,
    string JobNumber,
    string Status,
    string Mode,
    bool DryRun,
    bool Scheduled,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    DateTime? From,
    DateTime? To,
    IReadOnlyList<string> Modules,
    string? IdempotencyKey,
    bool StopOnError,
    int MaxAttempts,
    int RetryDelaySeconds,
    int SourceCount,
    int QueuedCount,
    int SkippedCount,
    int FailedCount,
    int DriftCount,
    string? LastCheckpoint,
    IReadOnlyList<FinalAccountsSyncJobItemDto> Items);

public sealed record FinalAccountsSyncJobListResponse(
    int TotalCount,
    IReadOnlyList<FinalAccountsSyncJobDto> Jobs);

public sealed record FinalAccountsReconciliationModuleDto(
    string Module,
    decimal SourceTotal,
    decimal JournalDebit,
    decimal JournalCredit,
    decimal Difference,
    int SourceCount,
    int PostedLinkCount,
    int ExceptionCount,
    string Status);

public sealed record FinalAccountsReconciliationResponse(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    DateTime? From,
    DateTime? To,
    decimal SourceTotal,
    decimal JournalDebit,
    decimal JournalCredit,
    decimal Difference,
    int ExceptionCount,
    IReadOnlyList<FinalAccountsReconciliationModuleDto> Modules);

namespace Garmetix.Api.SecondarySync;

public sealed record OracleSecondarySyncStatusDto(
    bool Enabled,
    bool Configured,
    bool OracleConfigured,
    string Direction,
    string ConflictPolicy,
    string TenantId,
    string SourceApplication,
    int IntervalSeconds,
    int BatchSize,
    string Schema,
    bool WalletConfigured,
    bool PullExternalEvents,
    bool ApplyInboundAutomatically,
    string[] EntityNames,
    string? LastRunUtc,
    string? LastSuccessUtc,
    string? LastError,
    bool IsRunning,
    string Note);

public sealed record OracleSecondarySyncRunRequest(string? EntityName = null, bool RepairFirst = true, string? Direction = null);

public sealed record OracleSecondarySyncRunResult(
    bool Success,
    string Message,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset FinishedAtUtc,
    int TotalPushed,
    int TotalPulled,
    IReadOnlyList<OracleEntitySyncResult> Entities,
    string? Error = null);

public sealed record OracleEntitySyncResult(
    string EntityName,
    int Scanned,
    int Pushed,
    int Pulled,
    string? CheckpointUtc,
    string? Error = null);

public sealed record OracleConnectionTestResult(
    bool Success,
    string Message,
    string? ServerTimeUtc = null,
    string? Error = null);

public sealed record OracleSyncHistoryRow(
    Guid Id,
    DateTime StartedAtUtc,
    DateTime? FinishedAtUtc,
    bool Success,
    int TotalPushed,
    int TotalPulled,
    string? Message,
    string? Error);

public sealed record OracleSyncInboundEventRow(
    Guid Id,
    string OracleEventId,
    string TenantId,
    string SourceApplication,
    string EntityName,
    string EntityId,
    string Operation,
    DateTime VersionUtc,
    string ConflictPolicy,
    string Status,
    string? Note,
    DateTime PulledAtUtc,
    DateTime? AppliedAtUtc,
    string? Error);

public sealed record OracleSyncDeadLetterRow(
    Guid Id,
    string Direction,
    string? OracleEventId,
    string SourceApplication,
    string EntityName,
    string EntityId,
    string Reason,
    string? Error,
    int RetryCount,
    bool Resolved,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record OracleDeadLetterActionResult(bool Success, string Message);


public sealed record OracleEntityOwnershipRow(
    string EntityName,
    string Owner,
    string InboundMode,
    string ConflictPolicy,
    bool CanApplyInbound,
    bool AutoApplyAllowed,
    string Notes);

public sealed record OracleInboundApplyRequest(bool Force = false, string? Note = null);

public sealed record OracleInboundApplyResult(
    bool Success,
    string Message,
    Guid InboundEventId,
    string EntityName,
    string EntityId,
    string Status,
    string? Error = null);

public sealed record OracleCloudReadinessDto(
    bool Enabled,
    bool ConnectionStringConfigured,
    bool WalletOrTnsConfigured,
    bool DirectionAllowsPull,
    bool DirectionAllowsPush,
    bool AutoApplyConfigured,
    string[] AutoApplyEntityNames,
    string[] TrustedSourceApplications,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> NextSteps);

public sealed record OracleAutoApplyPolicyRow(
    string EntityName,
    bool AutoApplyConfigured,
    bool OwnershipAllowsAutoApply,
    bool EffectiveAutoApply,
    string InboundMode,
    string ConflictPolicy,
    string Reason);

public sealed record OracleInboundAutoApplyRequest(string? EntityName = null, int? Take = null);

public sealed record OracleInboundAutoApplyResult(
    bool Success,
    string Message,
    int Scanned,
    int Applied,
    int Skipped,
    IReadOnlyList<OracleInboundApplyResult> Results);


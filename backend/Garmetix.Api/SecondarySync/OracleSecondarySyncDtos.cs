namespace Garmetix.Api.SecondarySync;

public sealed record OracleSecondarySyncStatusDto(
    bool Enabled,
    bool Configured,
    bool OracleConfigured,
    string Direction,
    string TenantId,
    string SourceApplication,
    int IntervalSeconds,
    int BatchSize,
    string Schema,
    string[] EntityNames,
    string? LastRunUtc,
    string? LastSuccessUtc,
    string? LastError,
    bool IsRunning,
    string Note);

public sealed record OracleSecondarySyncRunRequest(string? EntityName = null, bool RepairFirst = true);

public sealed record OracleSecondarySyncRunResult(
    bool Success,
    string Message,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset FinishedAtUtc,
    int TotalPushed,
    IReadOnlyList<OracleEntitySyncResult> Entities,
    string? Error = null);

public sealed record OracleEntitySyncResult(
    string EntityName,
    int Scanned,
    int Pushed,
    string? CheckpointUtc,
    string? Error = null);

public sealed record OracleConnectionTestResult(
    bool Success,
    string Message,
    string? ServerTimeUtc = null,
    string? Error = null);

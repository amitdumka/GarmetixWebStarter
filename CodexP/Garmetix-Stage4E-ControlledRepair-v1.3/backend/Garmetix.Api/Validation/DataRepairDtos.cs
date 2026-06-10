namespace Garmetix.Api.Validation;

public sealed record DataRepairActionDto(
    string Code,
    string Title,
    string Description,
    string RiskLevel,
    IReadOnlyList<string> FixesCheckCodes,
    bool RequiresConfirmation);

public sealed record DataRepairRequest(
    string ActionCode,
    int Limit = 100,
    bool Confirm = false,
    string? Reason = null);

public sealed record DataRepairChangeDto(
    string EntityType,
    Guid EntityId,
    string? ReferenceNumber,
    string Field,
    string? Before,
    string? After);

public sealed record DataRepairResultDto(
    string ActionCode,
    bool Applied,
    int ScannedRows,
    int AffectedRows,
    IReadOnlyList<DataRepairChangeDto> Changes,
    string Message,
    DateTimeOffset GeneratedAtUtc);

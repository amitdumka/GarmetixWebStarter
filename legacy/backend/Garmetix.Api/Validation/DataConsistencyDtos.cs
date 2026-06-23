namespace Garmetix.Api.Validation;

public sealed record DataConsistencyIssueDto(
    string Severity,
    string Area,
    string CheckCode,
    string Title,
    string Description,
    string? EntityType,
    Guid? EntityId,
    string? ReferenceNumber,
    Guid? CompanyId,
    Guid? StoreId,
    decimal? ExpectedValue,
    decimal? ActualValue,
    decimal? Difference);

public sealed record DataConsistencySectionDto(
    string Area,
    int TotalIssues,
    int CriticalIssues,
    int WarningIssues,
    int InfoIssues);

public sealed record DataConsistencySummaryDto(
    DateTimeOffset GeneratedAtUtc,
    int TotalIssues,
    int CriticalIssues,
    int WarningIssues,
    int InfoIssues,
    IReadOnlyList<DataConsistencySectionDto> Sections);

public sealed record DataConsistencyRunDto(
    DateTimeOffset GeneratedAtUtc,
    DataConsistencySummaryDto Summary,
    IReadOnlyList<DataConsistencyIssueDto> Issues);

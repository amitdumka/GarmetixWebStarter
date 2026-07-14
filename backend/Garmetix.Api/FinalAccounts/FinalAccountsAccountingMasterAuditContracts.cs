namespace Garmetix.Api.FinalAccounts;

public sealed record FinalAccountsAccountingMasterAuditQuery(Guid? CompanyId, Guid? StoreGroupId, Guid? StoreId);

public sealed record FinalAccountsAccountingMasterAuditResponse(
    DateTimeOffset GeneratedAtUtc,
    FinalAccountsScopeDto Scope,
    string StageName,
    bool WritesData,
    string BackupRequirement,
    IReadOnlyList<FinalAccountsAccountingMasterCountDto> MasterCounts,
    IReadOnlyList<FinalAccountsAccountingSourceCoverageDto> SourceCoverage,
    IReadOnlyList<FinalAccountsAccountingMasterIssueDto> Issues,
    IReadOnlyList<FinalAccountsAccountingDuplicateDto> Duplicates,
    IReadOnlyList<FinalAccountsAccountingRecommendationDto> Recommendations);

public sealed record FinalAccountsAccountingMasterCountDto(string Area, string Name, int Count, string Notes);

public sealed record FinalAccountsAccountingSourceCoverageDto(
    string SourceModule,
    int SourceRows,
    int LinkedFinalAccountsRows,
    int PendingRows,
    decimal SourceTotal,
    string Notes);

public sealed record FinalAccountsAccountingMasterIssueDto(
    string Severity,
    string Code,
    string Area,
    int Count,
    string Message,
    string SuggestedAction);

public sealed record FinalAccountsAccountingDuplicateDto(
    string Area,
    string Key,
    int Count,
    string Sample,
    string SuggestedAction);

public sealed record FinalAccountsAccountingRecommendationDto(string Stage, string Title, string Detail);

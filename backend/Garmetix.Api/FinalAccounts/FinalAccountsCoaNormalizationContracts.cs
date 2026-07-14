namespace Garmetix.Api.FinalAccounts;

public sealed record FinalAccountsCoaNormalizationPreviewQuery(Guid? CompanyId, Guid? StoreGroupId, Guid? StoreId);

public sealed record FinalAccountsCoaNormalizationPreviewResponse(
    DateTimeOffset GeneratedAtUtc,
    FinalAccountsScopeDto Scope,
    string StageName,
    bool WritesData,
    string BackupRequirement,
    IReadOnlyList<FinalAccountsCoaNormalizationSummaryDto> Summary,
    IReadOnlyList<FinalAccountsCoaGroupMappingDto> GroupMappings,
    IReadOnlyList<FinalAccountsCoaDuplicateGroupDto> DuplicateGroups,
    IReadOnlyList<FinalAccountsCoaControlAccountDto> ControlAccounts,
    IReadOnlyList<FinalAccountsCoaNormalizationIssueDto> Issues,
    IReadOnlyList<FinalAccountsCoaNormalizationStepDto> MigrationPlan,
    IReadOnlyList<FinalAccountsCoaNormalizationStepDto> RollbackPlan);

public sealed record FinalAccountsCoaNormalizationSummaryDto(string Name, int Count, string Notes);

public sealed record FinalAccountsCoaGroupMappingDto(
    Guid LedgerGroupId,
    string SourceName,
    string ExistingCategory,
    int LedgerCount,
    decimal OpeningBalance,
    string RecommendedPrimaryGroup,
    string RecommendedAccountType,
    string RecommendedNaturalBalance,
    int Confidence,
    string RuleCode,
    string SuggestedAction);

public sealed record FinalAccountsCoaDuplicateGroupDto(
    string NormalizedName,
    int Count,
    string SampleGroupIds,
    string SuggestedAction);

public sealed record FinalAccountsCoaControlAccountDto(
    string MappingKey,
    string RecommendedLedgerName,
    string RecommendedPrimaryGroup,
    string RecommendedAccountType,
    string ExistingLedgerId,
    string ExistingLedgerName,
    string Status,
    string Notes);

public sealed record FinalAccountsCoaNormalizationIssueDto(
    string Severity,
    string Code,
    int Count,
    string Message,
    string SuggestedAction);

public sealed record FinalAccountsCoaNormalizationStepDto(int StepNo, string Name, string Detail);

namespace Garmetix.Api.FinalAccounts;

public sealed record FinalAccountsPostingRuleDto(
    string SourceType,
    string RuleCode,
    string Version,
    string Name,
    string? Description,
    IReadOnlyList<FinalAccountsPostingRuleLineDto> Lines);

public sealed record FinalAccountsPostingRuleLineDto(
    string MappingKey,
    string DisplayName,
    string MappingCategory,
    string Direction,
    string? ExpectedAccountType,
    bool IsRequired,
    bool AllowControlAccount,
    int SortOrder,
    string? Notes);

public sealed record FinalAccountsMappingRequirementDto(
    string SourceType,
    string RuleCode,
    string RuleVersion,
    string MappingKey,
    string DisplayName,
    string MappingCategory,
    string Direction,
    string? ExpectedAccountType,
    bool IsRequired,
    bool AllowControlAccount,
    Guid? MappingId,
    Guid? AccountId,
    string? AccountCode,
    string? AccountName,
    string? AccountType,
    bool? AccountIsControl,
    bool MappingActive,
    string Status,
    string? IssueCode,
    string? IssueMessage);

public sealed record FinalAccountsMappingValidationResponse(
    int RuleCount,
    int RequirementCount,
    int MappedCount,
    int MissingCount,
    int IssueCount,
    IReadOnlyList<FinalAccountsMappingRequirementDto> Requirements,
    IReadOnlyList<FinalAccountsValidationIssueDto> Issues);

public sealed record FinalAccountsPostingPreviewLineRequest(
    string MappingKey,
    decimal Debit,
    decimal Credit,
    string? Narration);

public sealed record FinalAccountsPostingPreviewRequest(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string SourceType,
    string? RuleCode,
    string? RuleVersion,
    Guid? SourceId,
    string? SourceReference,
    string? SourceHash,
    IReadOnlyList<string>? MappingKeys,
    IReadOnlyList<FinalAccountsPostingPreviewLineRequest>? Lines);

public sealed record FinalAccountsPostingAdapterDto(
    string AdapterKey,
    string SourceType,
    string RuleCode,
    string RuleVersion,
    string DisplayName,
    string SourceTable,
    string Description);

public sealed record FinalAccountsPostingPreviewLineDto(
    string MappingKey,
    string DisplayName,
    string Direction,
    Guid? AccountId,
    string? AccountCode,
    string? AccountName,
    decimal Debit,
    decimal Credit,
    string? Narration);

public sealed record FinalAccountsPostingPreviewResponse(
    bool CanPost,
    string SourceType,
    string RuleCode,
    string RuleVersion,
    string SourceHash,
    string MappingVersion,
    IReadOnlyList<FinalAccountsPostingPreviewLineDto> Lines,
    IReadOnlyList<FinalAccountsValidationIssueDto> Issues);

public sealed record FinalAccountsPostingMappingSnapshot(string MappingKey, Guid AccountId, int Revision);

public sealed record FinalAccountsPostingRuleDefinition(
    string SourceType,
    string RuleCode,
    string Version,
    string Name,
    string? Description,
    IReadOnlyList<FinalAccountsPostingRuleLineDefinition> Lines);

public sealed record FinalAccountsPostingRuleLineDefinition(
    string MappingKey,
    string DisplayName,
    string MappingCategory,
    string Direction,
    string? ExpectedAccountType,
    bool IsRequired,
    bool AllowControlAccount,
    int SortOrder,
    string? Notes);

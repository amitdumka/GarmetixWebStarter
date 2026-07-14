namespace Garmetix.Api.FinalAccounts;

public sealed record FinalAccountsPartyLedgerUnificationPreviewQuery(Guid? CompanyId, Guid? StoreGroupId, Guid? StoreId);

public sealed record FinalAccountsPartyLedgerUnificationPreviewResponse(
    DateTimeOffset GeneratedAtUtc,
    FinalAccountsScopeDto Scope,
    string StageName,
    bool WritesData,
    string BackupRequirement,
    IReadOnlyList<FinalAccountsPartyLedgerSummaryDto> Summary,
    IReadOnlyList<FinalAccountsPartyRoleLinkDto> RoleLinks,
    IReadOnlyList<FinalAccountsPartyIdentityDto> Identities,
    IReadOnlyList<FinalAccountsPartyLedgerDuplicateDto> DuplicateParties,
    IReadOnlyList<FinalAccountsPartyLedgerIssueDto> Issues,
    IReadOnlyList<FinalAccountsPartyLedgerStepDto> UnificationPlan,
    IReadOnlyList<FinalAccountsPartyLedgerStepDto> RollbackPlan);

public sealed record FinalAccountsPartyLedgerSummaryDto(string Name, int Count, string Notes);

public sealed record FinalAccountsPartyRoleLinkDto(
    string Role,
    Guid SourceId,
    string SourceName,
    string IdentityKey,
    Guid? CurrentPartyId,
    string CurrentPartyName,
    Guid? CurrentLedgerId,
    string CurrentLedgerName,
    Guid? CandidatePartyId,
    string CandidatePartyName,
    Guid? CandidateLedgerId,
    string CandidateLedgerName,
    string Status,
    string SuggestedAction);

public sealed record FinalAccountsPartyIdentityDto(
    string IdentityKey,
    string DisplayName,
    string Roles,
    int SourceCount,
    int PartyCount,
    int LedgerCount,
    string Status,
    string SuggestedAction);

public sealed record FinalAccountsPartyLedgerDuplicateDto(
    string DuplicateKey,
    string Roles,
    int PartyCount,
    string PartyIds,
    string LedgerIds,
    string SuggestedAction);

public sealed record FinalAccountsPartyLedgerIssueDto(
    string Severity,
    string Code,
    int Count,
    string Message,
    string SuggestedAction);

public sealed record FinalAccountsPartyLedgerStepDto(int StepNo, string Name, string Detail);

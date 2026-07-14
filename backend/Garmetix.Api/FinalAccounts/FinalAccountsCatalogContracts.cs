using Garmetix.Core.Models.FinalAccounts;

namespace Garmetix.Api.FinalAccounts;

public sealed record FinalAccountsAccountGroupRequest(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    Guid? ParentGroupId,
    string Code,
    string Name,
    string AccountType,
    string NaturalBalance,
    int? SortOrder,
    bool? IsActive,
    string? Description);

public sealed record FinalAccountsAccountRequest(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    Guid AccountGroupId,
    Guid? ParentAccountId,
    string Code,
    string Name,
    string AccountType,
    string NaturalBalance,
    decimal? OpeningBalance,
    bool? IsControlAccount,
    bool? IsActive,
    string? Description,
    int? SortOrder);

public sealed record FinalAccountsAccountMappingRequest(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string SourceType,
    string MappingKey,
    string DisplayName,
    Guid AccountId,
    bool? IsRequired,
    bool? IsActive,
    string? Notes);

public sealed record FinalAccountsFiscalYearRequest(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    string? Status);

public sealed record FinalAccountsFiscalPeriodRequest(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    Guid FiscalYearId,
    int PeriodNumber,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    string? Status);

public sealed record FinalAccountsAccountGroupDto(
    Guid Id,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    Guid? ParentGroupId,
    string Code,
    string Name,
    string AccountType,
    string NaturalBalance,
    int SortOrder,
    bool IsSystem,
    bool IsActive,
    string? Description,
    int Revision,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record FinalAccountsAccountDto(
    Guid Id,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    Guid AccountGroupId,
    string? AccountGroupName,
    Guid? ParentAccountId,
    string Code,
    string Name,
    string AccountType,
    string NaturalBalance,
    decimal OpeningBalance,
    bool IsControlAccount,
    bool IsSystem,
    bool IsActive,
    string? Description,
    int SortOrder,
    int Revision,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record FinalAccountsAccountMappingDto(
    Guid Id,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string SourceType,
    string MappingKey,
    string DisplayName,
    Guid AccountId,
    string? AccountCode,
    string? AccountName,
    bool IsRequired,
    bool IsSystem,
    bool IsActive,
    string? Notes,
    int Revision);

public sealed record FinalAccountsFiscalYearDto(
    Guid Id,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    string Status,
    DateTime? ClosedAt,
    int Revision);

public sealed record FinalAccountsFiscalPeriodDto(
    Guid Id,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    Guid FiscalYearId,
    int PeriodNumber,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    string Status,
    DateTime? ClosedAt,
    int Revision);

public sealed record FinalAccountsSearchRowDto(Guid Id, string Code, string Name, string AccountType, string NaturalBalance);

public sealed record FinalAccountsValidationIssueDto(string Severity, string Code, string Message, Guid? EntityId);

public sealed record FinalAccountsValidationSummaryResponse(
    int GroupCount,
    int AccountCount,
    int MappingCount,
    int FiscalYearCount,
    int FiscalPeriodCount,
    IReadOnlyList<FinalAccountsValidationIssueDto> Issues);

public sealed record FinalAccountsSeedGroupDto(string Code, string Name, string AccountType, string NaturalBalance, string? ParentCode);
public sealed record FinalAccountsSeedAccountDto(string Code, string Name, string GroupCode, string AccountType, string NaturalBalance, bool IsControlAccount);
public sealed record FinalAccountsSeedPreviewResponse(
    string Template,
    IReadOnlyList<FinalAccountsSeedGroupDto> Groups,
    IReadOnlyList<FinalAccountsSeedAccountDto> Accounts,
    IReadOnlyList<FinalAccountsValidationIssueDto> Issues);

public sealed record FinalAccountsCatalogQuery(Guid? CompanyId, Guid? StoreGroupId, Guid? StoreId);

public sealed record ParsedAccountShape(FinalAccountsAccountType AccountType, FinalAccountsNaturalBalance NaturalBalance);

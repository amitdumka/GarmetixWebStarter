namespace Garmetix.Api.FinalAccounts;

public sealed record FinalAccountsTallyProfileQuery(Guid? CompanyId, Guid? StoreGroupId, Guid? StoreId, bool? ActiveOnly);

public sealed record FinalAccountsTallyProfileRequest(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string ProfileCode,
    string Name,
    string TallyRelease,
    string TestCompanyName,
    string BaseCurrency,
    string Country,
    string? GstRegistrationType,
    string DuplicatePolicy,
    bool DirectPostingAllowed,
    IReadOnlyDictionary<string, string>? GroupMapping,
    IReadOnlyDictionary<string, string>? LedgerMapping,
    IReadOnlyDictionary<string, string>? VoucherTypeMapping,
    IReadOnlyDictionary<string, string>? TaxMapping,
    IReadOnlyDictionary<string, string>? StockCostCentreMapping,
    bool IsActive,
    string? Notes);

public sealed record FinalAccountsTallyProfileDto(
    Guid Id,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string ProfileCode,
    string Name,
    string TallyRelease,
    string TestCompanyName,
    string BaseCurrency,
    string Country,
    string? GstRegistrationType,
    string DuplicatePolicy,
    bool DirectPostingAllowed,
    IReadOnlyDictionary<string, string> GroupMapping,
    IReadOnlyDictionary<string, string> LedgerMapping,
    IReadOnlyDictionary<string, string> VoucherTypeMapping,
    IReadOnlyDictionary<string, string> TaxMapping,
    IReadOnlyDictionary<string, string> StockCostCentreMapping,
    bool IsActive,
    string? Notes,
    int Revision);

public sealed record FinalAccountsExchangeRequest(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    Guid? TallyProfileId,
    DateTime From,
    DateTime To,
    DateTime? AsOf,
    string? Format,
    bool IncludeXmlFixture,
    bool IncludeJsonFixture,
    bool IncludeCaPackage,
    string? Notes);

public sealed record FinalAccountsCaPackageRequest(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    DateTime From,
    DateTime To,
    DateTime AsOf,
    string? EntityType,
    bool IncludeSupportingDocuments,
    bool IncludeTallyFixtures,
    string? Notes);

public sealed record FinalAccountsExchangeRunQuery(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string? RunKind,
    int? Page,
    int? PageSize);

public sealed record FinalAccountsExchangePreviewResponse(
    string RunKind,
    DateTime? From,
    DateTime? To,
    DateTime? AsOf,
    string Format,
    bool DirectPostingAllowed,
    string DuplicatePolicy,
    int MasterCount,
    int VoucherCount,
    decimal ControlDebit,
    decimal ControlCredit,
    decimal ControlDifference,
    IReadOnlyList<FinalAccountsExchangeMappingRowDto> Mappings,
    IReadOnlyList<FinalAccountsExchangeExceptionDto> Exceptions,
    string XmlFixture,
    string JsonFixture,
    IReadOnlyList<FinalAccountsCaPackageItemDto> PackageItems);

public sealed record FinalAccountsExchangeMappingRowDto(
    string MappingType,
    string SourceKey,
    string SourceName,
    string TallyName,
    string Status);

public sealed record FinalAccountsExchangeRunListResponse(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<FinalAccountsExchangeRunDto> Rows);

public sealed record FinalAccountsExchangeRunDto(
    Guid Id,
    string RunNumber,
    string RunKind,
    string Status,
    Guid? TallyProfileId,
    DateTime? PeriodFrom,
    DateTime? PeriodTo,
    DateTime? AsOf,
    string Format,
    int MasterCount,
    int VoucherCount,
    int ExceptionCount,
    decimal ControlDebit,
    decimal ControlCredit,
    string PayloadHash,
    string? ZipChecksum,
    string? FileName,
    DateTime CreatedAt,
    DateTime? GeneratedAt,
    string? GeneratedBy);

public sealed record FinalAccountsExchangeExceptionDto(
    string Severity,
    string Code,
    string Message,
    string? SourceType,
    Guid? SourceId);

public sealed record FinalAccountsCaPackageItemDto(
    string Path,
    string Category,
    string Description,
    string ContentType,
    int SizeBytes,
    string Sha256);

public sealed record FinalAccountsCaPackageManifestDto(
    string PackageNumber,
    DateTime GeneratedAt,
    DateTime From,
    DateTime To,
    DateTime AsOf,
    string Checksum,
    IReadOnlyList<FinalAccountsCaPackageItemDto> Items);

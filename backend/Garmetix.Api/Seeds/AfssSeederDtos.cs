namespace Garmetix.Api.Seeds;

public sealed record AfssSeedProfileDto(
    string Code,
    string Label,
    string CompanyName,
    string CompanyCode,
    string ContactPerson,
    string Gstin,
    string Pan,
    string StoreGroupCode,
    string StoreGroupName,
    string StoreCode,
    string StoreName,
    string City,
    string State,
    string ZipCode,
    string Email,
    string ContactNumber,
    string BaseCompanyUrl,
    string Source);

public sealed record AfssSeedCompanyDto(
    Guid Id,
    string Name,
    string Code,
    string Gstin,
    string ContactPerson,
    int StoreGroupCount,
    int StoreCount);

public sealed record AfssSeederOptionsDto(
    IReadOnlyList<AfssSeedProfileDto> Profiles,
    IReadOnlyList<AfssSeedCompanyDto> Companies,
    AfssSeederComparisonDto Comparison);

public sealed record AfssSeederComparisonDto(
    IReadOnlyList<string> CommonParts,
    IReadOnlyList<string> SeederCsOnly,
    IReadOnlyList<string> Seeder2CsOnly,
    IReadOnlyList<string> ModelAdjustmentsApplied);

public sealed record AfssSeedRequest(
    Guid CompanyId,
    string ProfileCode,
    bool IncludeUsers = true,
    bool IncludeEmployees = true,
    bool IncludeProducts = true,
    bool ResetDefaultUserPasswords = false);

public sealed record AfssSeedResponse(
    string Message,
    DateTimeOffset CompletedAtUtc,
    AfssSeedProfileDto Profile,
    AfssSeedTargetDto Target,
    AfssSeedCreatedCountsDto Created,
    AfssSeedExistingCountsDto Existing,
    IReadOnlyList<string> Notes);

public sealed record AfssSeedTargetDto(
    Guid CompanyId,
    string CompanyName,
    Guid StoreGroupId,
    string StoreGroupName,
    Guid StoreId,
    string StoreName);

public sealed record AfssSeedCreatedCountsDto(
    int StoreGroups,
    int Stores,
    int Banks,
    int Taxes,
    int Transactions,
    int LedgerGroups,
    int Ledgers,
    int BankAccounts,
    int Employees,
    int Salesmen,
    int Users,
    int ProductCategories,
    int ProductSubCategories,
    int Brands,
    int Vendors,
    int Products,
    int Stocks,
    int StockMovements,
    int ProductDetails);

public sealed record AfssSeedExistingCountsDto(
    int StoreGroups,
    int Stores,
    int Banks,
    int Taxes,
    int Transactions,
    int LedgerGroups,
    int Ledgers,
    int BankAccounts,
    int Employees,
    int Salesmen,
    int Users,
    int ProductCategories,
    int ProductSubCategories,
    int Brands,
    int Vendors,
    int Products,
    int Stocks,
    int StockMovements,
    int ProductDetails);

internal sealed class AfssSeedCounters
{
    public int StoreGroups { get; set; }
    public int Stores { get; set; }
    public int Banks { get; set; }
    public int Taxes { get; set; }
    public int Transactions { get; set; }
    public int LedgerGroups { get; set; }
    public int Ledgers { get; set; }
    public int BankAccounts { get; set; }
    public int Employees { get; set; }
    public int Salesmen { get; set; }
    public int Users { get; set; }
    public int ProductCategories { get; set; }
    public int ProductSubCategories { get; set; }
    public int Brands { get; set; }
    public int Vendors { get; set; }
    public int Products { get; set; }
    public int Stocks { get; set; }
    public int StockMovements { get; set; }
    public int ProductDetails { get; set; }

    public AfssSeedCreatedCountsDto ToCreatedDto() => new(
        StoreGroups,
        Stores,
        Banks,
        Taxes,
        Transactions,
        LedgerGroups,
        Ledgers,
        BankAccounts,
        Employees,
        Salesmen,
        Users,
        ProductCategories,
        ProductSubCategories,
        Brands,
        Vendors,
        Products,
        Stocks,
        StockMovements,
        ProductDetails);
}

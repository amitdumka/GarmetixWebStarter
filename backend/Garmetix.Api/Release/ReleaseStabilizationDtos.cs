using System.Text.Json.Serialization;

namespace Garmetix.Api.Release;

public sealed record ReleaseSmokeSummaryDto(
    string OverallStatus,
    DateTimeOffset CheckedAtUtc,
    int Passed,
    int Warnings,
    int Critical,
    IReadOnlyList<ReleaseSmokeCheckDto> Checks);

public sealed record ReleaseSmokeCheckDto(
    string Code,
    string Title,
    string Status,
    string Severity,
    string Message,
    string FixHint);

public sealed record DemoSeedRequest(
    bool CreateOnlyIfMissing = true,
    bool IncludeTrainingTransactions = false);

public sealed record DemoSeedResponse(
    string Message,
    DateTimeOffset CompletedAtUtc,
    DemoSeedCreatedCountsDto Created,
    DemoSeedIdsDto Ids,
    IReadOnlyList<string> Notes);

public sealed record DemoSeedCreatedCountsDto(
    int Companies,
    int StoreGroups,
    int Stores,
    int ProductCategories,
    int ProductSubCategories,
    int Taxes,
    int Vendors,
    int Customers,
    int Salesmen,
    int Brands,
    int Products,
    int Stocks,
    int StockMovements,
    int ProductDetails);

public sealed record DemoSeedIdsDto(
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId,
    Guid CategoryId,
    Guid SubCategoryId,
    Guid TaxId,
    Guid VendorId,
    Guid CustomerId,
    Guid SalesmanId);

internal sealed class DemoSeedCounters
{
    public int Companies { get; set; }
    public int StoreGroups { get; set; }
    public int Stores { get; set; }
    public int ProductCategories { get; set; }
    public int ProductSubCategories { get; set; }
    public int Taxes { get; set; }
    public int Vendors { get; set; }
    public int Customers { get; set; }
    public int Salesmen { get; set; }
    public int Brands { get; set; }
    public int Products { get; set; }
    public int Stocks { get; set; }
    public int StockMovements { get; set; }
    public int ProductDetails { get; set; }

    public DemoSeedCreatedCountsDto ToDto() => new(
        Companies,
        StoreGroups,
        Stores,
        ProductCategories,
        ProductSubCategories,
        Taxes,
        Vendors,
        Customers,
        Salesmen,
        Brands,
        Products,
        Stocks,
        StockMovements,
        ProductDetails);
}

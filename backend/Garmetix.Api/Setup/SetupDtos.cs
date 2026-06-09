namespace Garmetix.Api.Setup;

public sealed record SetupStatusResponse(
    bool HasCompany,
    bool HasStoreGroup,
    bool HasStore,
    bool HasProductCategory,
    bool HasTax,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId);

public sealed record QuickSetupRequest(
    string CompanyName,
    string StoreGroupName,
    string StoreName,
    string ContactNumber,
    string Email,
    string City,
    string State,
    string ZipCode);

public sealed record QuickSetupResponse(
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId,
    Guid ProductCategoryId,
    Guid ProductSubCategoryId,
    Guid TaxId);

public sealed record QuickProductRequest(
    string Name,
    string Barcode,
    decimal Mrp,
    decimal OpeningQuantity,
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId,
    Guid? ProductCategoryId,
    Guid? ProductSubCategoryId,
    Guid? TaxId,
    string? Descriptions = null,
    string? HSNCode = null,
    decimal? CostPrice = null,
    Garmetix.Core.Enums.Unit? Unit = null,
    Garmetix.Core.Enums.ProductType? ProductType = null,
    Garmetix.Core.Enums.ProductGroup? ProductGroup = null,
    Garmetix.Core.Enums.StockType? StockType = null);

public sealed record AccountingDefaultsResponse(
    int LedgerGroupsCreated,
    int LedgersCreated,
    int PartiesCreated);

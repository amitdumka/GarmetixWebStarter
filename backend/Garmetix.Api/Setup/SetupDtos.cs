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
    Guid? TaxId);

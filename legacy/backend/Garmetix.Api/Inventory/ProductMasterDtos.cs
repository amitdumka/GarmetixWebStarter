using Garmetix.Core.Enums;

namespace Garmetix.Api.Inventory;

public sealed class ProductMasterRequest
{
    public string Name { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string? Descriptions { get; set; }
    public string? HSNCode { get; set; }
    public decimal Mrp { get; set; }
    public decimal OpeningQuantity { get; set; }
    public decimal CostPrice { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? StoreGroupId { get; set; }
    public Guid? StoreId { get; set; }
    public Guid? TaxId { get; set; }
    public decimal? TaxRate { get; set; }
    public TaxType? TaxType { get; set; }
    public Unit? Unit { get; set; }
    public ProductType? ProductType { get; set; }
    public ProductGroup? ProductGroup { get; set; }
    public StockType? StockType { get; set; }
    public Guid? ProductCategoryId { get; set; }
    public Guid? ProductSubCategoryId { get; set; }
    public string? StyleCode { get; set; }
    public string? BaseColor { get; set; }
    public string? Brand { get; set; }
    public Guid? VendorId { get; set; }
}

public sealed record EnumOptionDto(int Value, string Label);
public sealed record IdNameOptionDto(Guid Id, string Name);
public sealed record ProductCategoryOptionDto(Guid Id, string Name, ProductGroup? ProductGroup, bool IsActive);
public sealed record ProductSubCategoryOptionDto(Guid Id, string Name, Guid? CategoryId);
public sealed record TaxOptionDto(Guid Id, string Name, decimal Rate, TaxType TaxType);
public sealed record VendorOptionDto(Guid Id, string Name, string? MobileNumber, string? GSTIN);

public sealed record ProductMasterOptionsResponse(
    IReadOnlyList<ProductCategoryOptionDto> Categories,
    IReadOnlyList<ProductSubCategoryOptionDto> SubCategories,
    IReadOnlyList<TaxOptionDto> Taxes,
    IReadOnlyList<VendorOptionDto> Vendors,
    IReadOnlyList<EnumOptionDto> Units,
    IReadOnlyList<EnumOptionDto> TaxTypes,
    IReadOnlyList<EnumOptionDto> ProductTypes,
    IReadOnlyList<EnumOptionDto> ProductGroups,
    IReadOnlyList<EnumOptionDto> StockTypes);

public sealed class ProductMasterRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string? Descriptions { get; set; }
    public string? HSNCode { get; set; }
    public decimal Mrp { get; set; }
    public decimal TaxRate { get; set; }
    public TaxType TaxType { get; set; }
    public Unit Unit { get; set; }
    public ProductType ProductType { get; set; }
    public ProductGroup ProductGroup { get; set; }
    public Guid ProductCategoryId { get; set; }
    public Guid ProductSubCategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? SubCategoryName { get; set; }
    public Guid? StockId { get; set; }
    public decimal PurchaseQty { get; set; }
    public decimal SoldQty { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal CostPrice { get; set; }
    public StockType StockType { get; set; }
    public Guid? TaxId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid StoreGroupId { get; set; }
    public Guid? StoreId { get; set; }
    public string? StyleCode { get; set; }
    public string? BaseColor { get; set; }
    public string? Brand { get; set; }
    public Guid? VendorId { get; set; }
}

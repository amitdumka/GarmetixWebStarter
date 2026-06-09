namespace Garmetix.Api.ProductLookup;

public sealed record ProductLookupRow(
    Guid ProductId,
    Guid? StockId,
    string Name,
    string Barcode,
    string? HsnCode,
    decimal AvailableQty,
    decimal Mrp,
    decimal TaxRate,
    string TaxType,
    string Unit,
    string Category,
    string SubCategory,
    Guid? TaxId,
    Guid? ProductCategoryId,
    Guid? ProductSubCategoryId);

public sealed record ScanLookupResult(
    string Code,
    string EntityType,
    Guid EntityId,
    string Number,
    DateTime OnDate,
    string PartyName,
    decimal Amount,
    string Url);

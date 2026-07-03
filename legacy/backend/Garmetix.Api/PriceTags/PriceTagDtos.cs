namespace Garmetix.Api.PriceTags;

public sealed record PriceTagStockRowDto(
    Guid StockId,
    Guid ProductId,
    Guid StoreId,
    string ProductName,
    string Barcode,
    string? HsnCode,
    string Unit,
    decimal CurrentStock,
    decimal CostPrice,
    decimal Mrp,
    decimal TaxRate,
    string? CategoryName,
    string? SubCategoryName,
    string? Brand,
    string? StoreName);

public sealed record PriceTagPurchaseInwardDto(
    Guid Id,
    string InvoiceNumber,
    string InwardNumber,
    string VendorName,
    DateTime InwardDate,
    DateTime? SupplierInvoiceDate,
    decimal BillAmount);

public sealed record PriceTagRequest(
    string LabelSize,
    string PrinterLanguage,
    string StoreName,
    IReadOnlyList<PriceTagRequestRow> Rows);

public sealed record PriceTagRequestRow(
    Guid? StockId,
    Guid? ProductId,
    string ProductName,
    string Barcode,
    string? HsnCode,
    string? Brand,
    decimal Mrp,
    decimal Copies);

public sealed record PriceTagPreviewDto(
    string LabelSize,
    string PrinterLanguage,
    int LabelCount,
    IReadOnlyList<PriceTagLabelDto> Labels,
    string ThermalCommands,
    IReadOnlyList<string> Warnings);

public sealed record PriceTagLabelDto(
    string ProductName,
    string Barcode,
    string? HsnCode,
    string? Brand,
    decimal Mrp,
    int CopyNumber);

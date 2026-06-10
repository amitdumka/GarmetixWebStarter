using Garmetix.Core.Enums;

namespace Garmetix.Api.NonGstGoods;

public sealed record NonGstGoodsOptionsDto(
    IReadOnlyList<NonGstStockOptionDto> Stocks,
    IReadOnlyList<NonGstPartyOptionDto> Vendors,
    IReadOnlyList<NonGstPartyOptionDto> Customers,
    IReadOnlyList<NonGstLedgerOptionDto> Ledgers,
    IReadOnlyList<NonGstDocumentRowDto> RecentDocuments);

public sealed record NonGstStockOptionDto(
    Guid ProductId,
    Guid StockId,
    string ProductName,
    string Barcode,
    decimal CurrentStock,
    decimal CostPrice,
    decimal MRP,
    Guid StoreId,
    string StoreName,
    string Label);

public sealed record NonGstPartyOptionDto(Guid Id, string Name, string? Mobile, string? GSTIN);
public sealed record NonGstLedgerOptionDto(Guid Id, string Name, string GroupName, string LedgerType);

public sealed record NonGstGoodsRequest(
    DateTime? OnDate,
    Guid? VendorId,
    Guid? CustomerId,
    string? PartyName,
    PaymentMode PaymentMode,
    string? ReferenceNumber,
    string? Remarks,
    IReadOnlyList<NonGstGoodsItemRequest> Items);

public sealed record NonGstGoodsItemRequest(
    Guid? StockId,
    Guid? ProductId,
    string? ProductName,
    string? Barcode,
    decimal Quantity,
    decimal Rate,
    decimal DiscountAmount,
    decimal? CostPrice,
    decimal? MRP,
    Guid? StoreId);

public sealed record NonGstGoodsResponse(
    Guid Id,
    string DocumentNumber,
    string DocumentType,
    DateTime OnDate,
    string PartyName,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal NetAmount,
    string Message);

public sealed record NonGstDocumentRowDto(
    Guid Id,
    string DocumentNumber,
    string DocumentType,
    DateTime OnDate,
    string PartyName,
    int ItemCount,
    decimal Quantity,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal NetAmount,
    decimal CostAmount,
    decimal ProfitAmount,
    string? Remarks);

public sealed record NonGstReportStockRowDto(
    Guid StockId,
    Guid ProductId,
    string ProductName,
    string Barcode,
    Guid StoreId,
    string StoreName,
    decimal PurchaseQty,
    decimal SoldQty,
    decimal CurrentStock,
    decimal CostPrice,
    decimal Mrp,
    decimal StockValue);

public sealed record NonGstReportDto(
    DateTime From,
    DateTime To,
    int PurchaseCount,
    decimal PurchaseQty,
    decimal PurchaseAmount,
    int SaleCount,
    decimal SaleQty,
    decimal SaleAmount,
    decimal DiscountAmount,
    decimal SaleCostAmount,
    decimal GrossProfit,
    decimal CurrentStockQty,
    decimal CurrentStockValue,
    IReadOnlyList<NonGstDocumentRowDto> Rows,
    IReadOnlyList<NonGstReportStockRowDto> CurrentStockRows);

public sealed record NonGstPrintDto(
    Guid Id,
    string DocumentNumber,
    string DocumentType,
    DateTime OnDate,
    string PartyName,
    string? ReferenceNumber,
    string PaymentMode,
    string? Remarks,
    string CompanyName,
    string StoreName,
    string StoreAddress,
    string? StorePhone,
    string? StoreEmail,
    string TaxNote,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal TaxableAmount,
    decimal TaxAmount,
    decimal NetAmount,
    decimal CostAmount,
    decimal ProfitAmount,
    IReadOnlyList<NonGstPrintItemDto> Items);

public sealed record NonGstPrintItemDto(
    int Serial,
    string ProductName,
    string Barcode,
    decimal Quantity,
    decimal Rate,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal TaxableAmount,
    decimal TaxRate,
    decimal TaxAmount,
    decimal Amount,
    decimal CostRate,
    decimal CostAmount,
    decimal ProfitAmount);

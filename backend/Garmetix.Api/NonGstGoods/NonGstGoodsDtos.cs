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
    decimal Quantity,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal NetAmount,
    string? Remarks);

public sealed record NonGstReportDto(
    DateTime From,
    DateTime To,
    decimal PurchaseQty,
    decimal PurchaseAmount,
    decimal SaleQty,
    decimal SaleAmount,
    decimal DiscountAmount,
    decimal NetExtraIncome,
    IReadOnlyList<NonGstDocumentRowDto> Rows);

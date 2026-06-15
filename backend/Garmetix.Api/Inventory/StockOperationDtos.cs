using Garmetix.Core.Enums;

namespace Garmetix.Api.Inventory;

public sealed record StockOperationOptionsDto(
    IReadOnlyList<StockOperationProductOptionDto> Products,
    IReadOnlyList<StockOperationStoreOptionDto> Stores,
    IReadOnlyList<StockMovementRowDto> RecentMovements);

public sealed record StockOperationProductOptionDto(
    Guid ProductId,
    Guid StockId,
    string ProductName,
    string Barcode,
    string? HSNCode,
    Unit Unit,
    decimal CurrentStock,
    decimal CostPrice,
    decimal MRP,
    decimal TaxRate,
    TaxType TaxType,
    Guid TaxId,
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId,
    string StoreName,
    string Label);

public sealed record StockOperationStoreOptionDto(
    Guid Id,
    string Name,
    Guid CompanyId,
    Guid StoreGroupId);

public sealed record StockAdjustmentRequest(
    Guid StockId,
    decimal Quantity,
    string Direction,
    string? Reason);

public sealed record StockTransferRequest(
    Guid FromStockId,
    Guid ToStoreId,
    decimal Quantity,
    string? Reason);

public sealed record PhysicalStockCountRequest(
    Guid StockId,
    decimal CountedQuantity,
    string? Reason);

public sealed record StockOperationResponse(
    Guid DocumentId,
    string OperationNumber,
    Guid ProductId,
    string Barcode,
    Guid StoreId,
    decimal QuantityIn,
    decimal QuantityOut,
    decimal CurrentStock,
    string MovementType,
    string Message);

public sealed record StockTransferResponse(
    Guid DocumentId,
    string OperationNumber,
    Guid ProductId,
    string Barcode,
    Guid FromStoreId,
    Guid ToStoreId,
    decimal Quantity,
    decimal FromStoreCurrentStock,
    decimal ToStoreCurrentStock,
    string Message);

public sealed record StockMovementRowDto(
    Guid Id,
    DateTime OnDate,
    string MovementType,
    string ProductName,
    string Barcode,
    string StoreName,
    decimal QuantityIn,
    decimal QuantityOut,
    decimal MRP,
    decimal CostPrice,
    string? SourceNumber,
    string? Remarks);

public sealed record StockOperationDocumentRowDto(
    Guid Id,
    string DocumentNumber,
    DateTime OnDate,
    string OperationType,
    string Status,
    string? FromStoreName,
    string? ToStoreName,
    decimal TotalQuantity,
    decimal TotalCostValue,
    decimal TotalMrpValue,
    int ItemCount,
    string Reason);

public sealed record StockOperationDocumentDetailDto(
    Guid Id,
    string DocumentNumber,
    DateTime OnDate,
    string OperationType,
    string Status,
    Guid? FromStoreId,
    string? FromStoreName,
    Guid? ToStoreId,
    string? ToStoreName,
    decimal TotalQuantity,
    decimal TotalCostValue,
    decimal TotalMrpValue,
    int ItemCount,
    string Reason,
    DateTime PostedAt,
    IReadOnlyList<StockOperationItemDto> Items);

public sealed record StockOperationItemDto(
    Guid Id,
    Guid ProductId,
    Guid? StockId,
    Guid? DestinationStockId,
    string ProductName,
    string Barcode,
    string? HsnCode,
    string Unit,
    decimal SystemQuantity,
    decimal? CountedQuantity,
    decimal QuantityIn,
    decimal QuantityOut,
    decimal QuantityDifference,
    decimal FromQuantityBefore,
    decimal FromQuantityAfter,
    decimal? ToQuantityBefore,
    decimal? ToQuantityAfter,
    decimal CostPrice,
    decimal Mrp,
    decimal CostValue,
    decimal MrpValue,
    Guid? OutMovementId,
    Guid? InMovementId,
    string? Reason);

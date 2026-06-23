namespace Garmetix.Api.Inventory;

public sealed record StockReportSummaryDto(
    DateTime AsOf,
    decimal LowStockThreshold,
    int StockRows,
    decimal TotalQuantity,
    decimal TotalInventoryValue,
    int LowStockRows,
    int AgedOver90DaysRows,
    int ReconciliationMismatchRows,
    int PendingAccountingDocuments,
    IReadOnlyList<StockReportBucketDto> AgeBuckets,
    IReadOnlyList<StockReportBucketDto> RiskBuckets,
    IReadOnlyList<StockReportRowDto> Rows);

public sealed record StockReportBucketDto(
    string Label,
    int Rows,
    decimal Quantity,
    decimal InventoryValue);

public sealed record StockReportRowDto(
    Guid StockId,
    Guid ProductId,
    string ProductName,
    string Barcode,
    string StoreName,
    decimal LedgerQuantity,
    decimal ProjectedQuantity,
    decimal AverageCost,
    decimal ProjectedAverageCost,
    decimal InventoryValue,
    DateTime? LastInwardAt,
    DateTime? LastMovementAt,
    int? AgeDays,
    string AgeBucket,
    string Risk,
    string ReconciliationStatus,
    int MovementCount);


public sealed record StockMovementHistorySummaryDto(
    DateTime AsOf,
    Guid? StockId,
    Guid? ProductId,
    string ProductName,
    string Barcode,
    string StoreName,
    decimal CurrentQuantity,
    decimal CurrentAverageCost,
    decimal CurrentStockValue,
    decimal TotalPurchasedQuantity,
    decimal TotalPurchaseReturnQuantity,
    decimal TotalSoldQuantity,
    decimal TotalSalesReturnQuantity,
    decimal TotalSalesAmount,
    decimal TotalCostOfGoodsSold,
    decimal GrossProfitOrLoss,
    decimal TotalPositiveProfit,
    decimal TotalLoss,
    IReadOnlyList<StockMovementHistoryRowDto> Rows);

public sealed record StockMovementHistoryRowDto(
    Guid MovementId,
    DateTime OnDate,
    string ProductName,
    string Barcode,
    string MovementType,
    string MovementLabel,
    string Direction,
    string? SourceType,
    Guid? SourceId,
    string? SourceNumber,
    decimal QuantityIn,
    decimal QuantityOut,
    decimal NetQuantity,
    decimal UnitCost,
    decimal? UnitSalePrice,
    decimal CostAmount,
    decimal? SalesAmount,
    decimal ProfitOrLoss,
    decimal QuantityBefore,
    decimal QuantityAfter,
    decimal AverageCostBefore,
    decimal AverageCostAfter,
    decimal InventoryValueBefore,
    decimal InventoryValueAfter,
    string? Remarks);

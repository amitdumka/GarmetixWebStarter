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

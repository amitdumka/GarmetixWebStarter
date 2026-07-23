namespace Garmetix.Api.Inventory;

public record StockAuditPeriodRowDto(
    Guid Id,
    string Name,
    Guid StoreId,
    string StoreName,
    DateTime StartDate,
    DateTime EndDate,
    string Status,
    string? Notes,
    DateTime? ClosedAt,
    DateTime CreatedAt,
    int ScannedBarcodeCount,
    decimal TotalScannedQuantity);

public record CreateStockAuditPeriodRequest(string Name, Guid StoreId, DateTime StartDate, DateTime EndDate, string? Notes);

public record UpdateStockAuditPeriodRequest(string Name, DateTime StartDate, DateTime EndDate, string? Notes);

public record RecordStockAuditScanRequest(Guid AuditPeriodId, string Barcode, decimal Quantity, DateTime? ScanDate);

public record StockAuditScanRowDto(
    Guid Id,
    Guid AuditPeriodId,
    string Barcode,
    string ProductName,
    string? CategoryName,
    string? SubCategoryName,
    string? Color,
    string? Size,
    string? Unit,
    DateTime ScanDate,
    decimal Quantity,
    int ScanCount,
    DateTime FirstScannedAt,
    DateTime LastScannedAt,
    decimal MRP,
    decimal CostPrice);

public record RecordStockAuditScanResponse(StockAuditScanRowDto Scan, decimal TodayQuantityForBarcode, decimal PeriodTotalQuantityForBarcode);

public record StockAuditScanLogResponseDto(int Total, int Page, int PageSize, IReadOnlyList<StockAuditScanRowDto> Rows);

public record StockAuditScanSummaryRowDto(
    string Barcode,
    string ProductName,
    string? CategoryName,
    string? SubCategoryName,
    string? Color,
    string? Size,
    string? Unit,
    decimal MRP,
    decimal CostPrice,
    decimal TotalQuantity,
    IReadOnlyList<StockAuditScanDateEntryDto> DateEntries);

public record StockAuditScanDateEntryDto(DateTime ScanDate, decimal Quantity, int ScanCount);

public record StockAuditDiscrepancyRowDto(
    string Barcode,
    string ProductName,
    string? CategoryName,
    string? SubCategoryName,
    string? Color,
    string? Size,
    string? Unit,
    decimal AuditedQuantity,
    decimal CurrentQuantity,
    decimal Variance,
    decimal MRP,
    decimal CostPrice,
    decimal AuditedMrpValue,
    decimal CurrentMrpValue,
    decimal AuditedCostValue,
    decimal CurrentCostValue,
    string Status);

public record StockAuditDiscrepancySummaryDto(
    int TotalItems,
    int MatchedCount,
    int MismatchCount,
    int NotCountedCount,
    int MissingInSystemCount,
    decimal TotalVarianceQuantity,
    decimal TotalAuditedMrpValue,
    decimal TotalCurrentMrpValue,
    decimal TotalAuditedCostValue,
    decimal TotalCurrentCostValue);

public record StockAuditDiscrepancyResponseDto(
    Guid AuditPeriodId,
    string AuditPeriodName,
    StockAuditDiscrepancySummaryDto Summary,
    IReadOnlyList<StockAuditDiscrepancyRowDto> Rows);

public record StockSummaryRowDto(
    string CategoryName,
    string ColorName,
    string SizeName,
    int ProductCount,
    decimal Quantity,
    decimal MrpValue,
    decimal CostValue);

public record StockSummaryResponseDto(
    string Source,
    string GroupBy,
    string? GroupValue,
    Guid? AuditPeriodId,
    string? AuditPeriodName,
    Guid? StoreId,
    int TotalProductCount,
    decimal TotalQuantity,
    decimal TotalMrpValue,
    decimal TotalCostValue,
    IReadOnlyList<string> AvailableGroupValues,
    IReadOnlyList<StockSummaryRowDto> Rows);

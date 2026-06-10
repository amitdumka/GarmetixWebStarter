namespace Garmetix.Api.Dashboard;

public sealed record DashboardMetricDto(
    string Label,
    decimal Value,
    string DisplayValue,
    string Caption,
    string Icon,
    string Color);

public sealed record DashboardTrendPointDto(
    string Label,
    DateTime Date,
    decimal Sales,
    decimal Purchase);

public sealed record DashboardActivityDto(
    string Title,
    string Subtitle,
    string Amount,
    DateTime OnDate,
    string Status,
    string Resource,
    Guid? ResourceId);

public sealed record StorePerformanceDto(
    Guid StoreId,
    string StoreName,
    decimal SalesMonth,
    decimal PurchaseMonth,
    decimal StockValue,
    int InvoiceCount,
    decimal CurrentStockQty);

public sealed record DashboardScopeDto(
    string ScopeType,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string CompanyName,
    string StoreGroupName,
    string StoreName);

public sealed record StoreManagerDashboardDto(
    DashboardScopeDto Scope,
    IReadOnlyList<DashboardMetricDto> Metrics,
    IReadOnlyList<DashboardTrendPointDto> Trend,
    IReadOnlyList<DashboardActivityDto> RecentSales,
    IReadOnlyList<DashboardActivityDto> StockAlerts,
    IReadOnlyList<DashboardActivityDto> WorkQueue);

public sealed record BusinessDashboardDto(
    DashboardScopeDto Scope,
    IReadOnlyList<DashboardMetricDto> Metrics,
    IReadOnlyList<DashboardTrendPointDto> Trend,
    IReadOnlyList<StorePerformanceDto> Stores,
    IReadOnlyList<DashboardActivityDto> RecentSales,
    IReadOnlyList<DashboardActivityDto> RecentPurchases,
    IReadOnlyList<DashboardActivityDto> AdminQueue);

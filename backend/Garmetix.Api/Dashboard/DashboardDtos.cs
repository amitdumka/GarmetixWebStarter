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

public sealed record StoreGroupPerformanceDto(
    Guid StoreGroupId,
    string StoreGroupName,
    int StoreCount,
    decimal SalesMonth,
    decimal PurchaseMonth,
    decimal StockValue,
    int InvoiceCount,
    decimal CurrentStockQty);

public sealed record DashboardQuickActionDto(
    string Label,
    string Description,
    string To,
    string Icon,
    string Color,
    bool Attention);

public sealed record DashboardHealthSignalDto(
    string Label,
    string Value,
    string Status,
    string Description,
    string Icon,
    string Color);

public sealed record DashboardPeriodDto(
    string Label,
    DateTime FromDate,
    DateTime ToDate,
    int Days,
    string Preset);

public sealed record DashboardHomeDto(
    string Route,
    string DashboardType,
    string Reason,
    bool CanOpenBusinessDashboard,
    bool CanOpenStoreManagerDashboard);

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
    IReadOnlyList<DashboardActivityDto> WorkQueue,
    IReadOnlyList<DashboardQuickActionDto> QuickActions,
    IReadOnlyList<DashboardHealthSignalDto> HealthSignals,
    DashboardPeriodDto Period);

public sealed record BusinessDashboardDto(
    DashboardScopeDto Scope,
    IReadOnlyList<DashboardMetricDto> Metrics,
    IReadOnlyList<DashboardTrendPointDto> Trend,
    IReadOnlyList<StorePerformanceDto> Stores,
    IReadOnlyList<StoreGroupPerformanceDto> StoreGroups,
    IReadOnlyList<DashboardActivityDto> RecentSales,
    IReadOnlyList<DashboardActivityDto> RecentPurchases,
    IReadOnlyList<DashboardActivityDto> AdminQueue,
    IReadOnlyList<DashboardQuickActionDto> QuickActions,
    IReadOnlyList<DashboardHealthSignalDto> HealthSignals,
    DashboardPeriodDto Period);

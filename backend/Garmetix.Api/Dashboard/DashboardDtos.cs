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
    decimal Purchase,
    decimal Profit,
    decimal NonGstSales,
    decimal NonGstPurchase);

public sealed record DashboardBreakdownDto(
    string Label,
    decimal Value,
    string DisplayValue,
    string Color,
    string Icon,
    string Caption);

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


public sealed record PartyDueDashboardRowDto(
    string PartyType,
    Guid PartyId,
    string PartyName,
    string Contact,
    int BillCount,
    decimal BillAmount,
    decimal PaidAmount,
    decimal DueAmount,
    DateTime? OldestBillDate,
    string AgeBucket);

public sealed record PaymentModeSummaryDto(
    string PaymentMode,
    decimal SalesCollection,
    decimal PurchasePayment,
    decimal VoucherReceipt,
    decimal VoucherPayment,
    decimal NetAmount,
    int TransactionCount);

public sealed record CashPaymentSummaryDto(
    decimal CashIn,
    decimal CashOut,
    decimal BankIn,
    decimal BankOut,
    decimal NetCash,
    IReadOnlyList<PaymentModeSummaryDto> PaymentModes);

public sealed record StoreGroupComparisonViewDto(
    Guid StoreGroupId,
    string StoreGroupName,
    int StoreCount,
    decimal Sales,
    decimal Purchase,
    decimal CustomerDue,
    decimal VendorDue,
    decimal CashIn,
    decimal CashOut,
    decimal NetCash,
    decimal StockValue);

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
    IReadOnlyList<DashboardBreakdownDto> RevenueBreakdown,
    IReadOnlyList<DashboardBreakdownDto> StockBreakdown,
    IReadOnlyList<DashboardBreakdownDto> ProfitBreakdown,
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
    IReadOnlyList<DashboardBreakdownDto> RevenueBreakdown,
    IReadOnlyList<DashboardBreakdownDto> StockBreakdown,
    IReadOnlyList<DashboardBreakdownDto> ProfitBreakdown,
    DashboardPeriodDto Period,
    IReadOnlyList<PartyDueDashboardRowDto> CustomerDues,
    IReadOnlyList<PartyDueDashboardRowDto> VendorDues,
    CashPaymentSummaryDto CashPaymentSummary,
    IReadOnlyList<StoreGroupComparisonViewDto> StoreGroupComparison);

public sealed record TodayCashFlowDto(
    decimal SalesCollections,
    decimal PurchasePayments,
    decimal VoucherReceipts,
    decimal VoucherPayments,
    decimal VoucherExpenses,
    decimal CashVoucherReceipts,
    decimal CashVoucherPayments,
    decimal CashVoucherExpenses,
    decimal TotalReceipts,
    decimal TotalPayments,
    decimal TotalExpenses,
    decimal NetCashFlow);

public sealed record TodayEmployeeAttendanceDto(
    Guid EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    string Department,
    string Designation,
    string Status,
    DateTime? FirstPunchAt,
    DateTime? LastPunchAt,
    string LastPunchType,
    string Source);

public sealed record TodayAttendanceSummaryDto(
    int ActiveEmployees,
    int PresentEmployees,
    int AbsentEmployees,
    int PendingReviewEmployees,
    IReadOnlyList<TodayEmployeeAttendanceDto> Present,
    IReadOnlyList<TodayEmployeeAttendanceDto> Absent);

public sealed record TodayDashboardDto(
    DashboardScopeDto Scope,
    DateTime BusinessDate,
    IReadOnlyList<DashboardMetricDto> Metrics,
    IReadOnlyList<DashboardTrendPointDto> SalesTrend,
    TodayCashFlowDto CashFlow,
    TodayAttendanceSummaryDto Attendance,
    IReadOnlyList<DashboardActivityDto> RecentActivities,
    IReadOnlyList<DashboardQuickActionDto> QuickActions);


namespace Garmetix.Api.FinalAccounts;

public sealed record FinalAccountsProjectionScenarioQuery(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string? Status,
    string? ScenarioType,
    int? Page,
    int? PageSize);

public sealed record FinalAccountsProjectionScenarioSaveRequest(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string Name,
    string? Description,
    string ScenarioType,
    string BaselineSource,
    DateTime? BaselineFrom,
    DateTime? BaselineTo,
    DateTime ProjectionStart,
    int HorizonMonths,
    FinalAccountsProjectionBaselineDto Baseline,
    FinalAccountsProjectionAssumptionDto Assumptions);

public sealed record FinalAccountsProjectionCloneRequest(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string Name,
    string? Description,
    string ScenarioType);

public sealed record FinalAccountsProjectionWorkflowRequest(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string? Notes);

public sealed record FinalAccountsProjectionActualBaselineRequest(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    DateTime From,
    DateTime To,
    string? EntityType);

public sealed record FinalAccountsProjectionCompareRequest(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    IReadOnlyList<Guid> ScenarioIds);

public sealed record FinalAccountsProjectionScenarioListResponse(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<FinalAccountsProjectionScenarioListRowDto> Rows);

public sealed record FinalAccountsProjectionScenarioListRowDto(
    Guid Id,
    string ScenarioNumber,
    string Name,
    string ScenarioType,
    string Status,
    string BaselineSource,
    DateTime ProjectionStart,
    int HorizonMonths,
    int ActiveAssumptionVersion,
    decimal TotalRevenue,
    decimal ProfitAfterTax,
    decimal ClosingCash,
    decimal BalanceDifference,
    DateTime CreatedAt,
    DateTime? ApprovedAt,
    DateTime? ArchivedAt);

public sealed record FinalAccountsProjectionScenarioDto(
    Guid Id,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string ScenarioNumber,
    string Name,
    string? Description,
    string ScenarioType,
    string Status,
    string BaselineSource,
    DateTime? BaselineFrom,
    DateTime? BaselineTo,
    DateTime ProjectionStart,
    int HorizonMonths,
    int ActiveAssumptionVersion,
    FinalAccountsProjectionBaselineDto Baseline,
    FinalAccountsProjectionAssumptionDto Assumptions,
    FinalAccountsProjectionSummaryDto Summary,
    IReadOnlyList<FinalAccountsProjectionMonthDto> Months,
    IReadOnlyList<FinalAccountsProjectionEventDto> Events,
    string? DecisionNotes,
    int Revision);

public sealed record FinalAccountsProjectionBaselineDto(
    decimal MonthlyRevenue,
    decimal MonthlyGrossProfit,
    decimal MonthlyProfitAfterTax,
    decimal Cash,
    decimal Inventory,
    decimal Debtors,
    decimal Creditors,
    decimal FixedAssets,
    decimal Debt,
    decimal Capital);

public sealed record FinalAccountsProjectionAssumptionDto(
    decimal RevenueGrowthPercent,
    IReadOnlyList<decimal> SeasonalityFactors,
    decimal NewStoreMonthlyRevenue,
    decimal AverageBillValue,
    decimal CustomerCountGrowthPercent,
    decimal ReturnsDiscountPercent,
    decimal GrossMarginPercent,
    decimal PurchaseInflationPercent,
    decimal InventoryDays,
    decimal DebtorDays,
    decimal CreditorDays,
    decimal EmployeeCostMonthly,
    decimal SalaryGrowthPercent,
    decimal RentExpenseMonthly,
    decimal ExpenseEscalationPercent,
    decimal CapexMonthly,
    decimal DepreciationRatePercent,
    decimal DebtOpening,
    decimal InterestRatePercent,
    decimal DebtRepaymentMonthly,
    decimal CapitalInjectionMonthly,
    decimal DrawingsMonthly,
    decimal TaxRatePercent,
    decimal MinimumCash,
    string? Notes);

public sealed record FinalAccountsProjectionMonthDto(
    int MonthNumber,
    DateTime MonthStart,
    DateTime MonthEnd,
    decimal Revenue,
    decimal ReturnsAndDiscounts,
    decimal NetRevenue,
    decimal CostOfGoodsSold,
    decimal GrossProfit,
    decimal PayrollExpense,
    decimal RentExpense,
    decimal OtherExpense,
    decimal Depreciation,
    decimal Interest,
    decimal Tax,
    decimal ProfitAfterTax,
    decimal InventoryBalance,
    decimal DebtorBalance,
    decimal CreditorBalance,
    decimal FixedAssets,
    decimal DebtBalance,
    decimal CapitalBalance,
    decimal CashBalance,
    decimal TotalAssets,
    decimal TotalLiabilitiesEquity,
    decimal BalanceDifference,
    decimal OperatingCashFlow,
    decimal InvestingCashFlow,
    decimal FinancingCashFlow,
    decimal ClosingCashFlow,
    decimal WorkingCapitalRequirement,
    decimal BreakEvenRevenue,
    decimal CurrentRatio,
    decimal DebtEquityRatio);

public sealed record FinalAccountsProjectionSummaryDto(
    decimal TotalRevenue,
    decimal TotalGrossProfit,
    decimal TotalProfitAfterTax,
    decimal ClosingCash,
    decimal ClosingDebt,
    decimal ClosingWorkingCapitalRequirement,
    decimal MaxBalanceDifference,
    decimal AverageCurrentRatio,
    decimal EndingDebtEquityRatio,
    string BalanceStatus);

public sealed record FinalAccountsProjectionComparisonResponse(
    IReadOnlyList<FinalAccountsProjectionComparisonRowDto> Rows,
    IReadOnlyList<FinalAccountsProjectionComparisonMonthDto> Months);

public sealed record FinalAccountsProjectionComparisonRowDto(
    Guid ScenarioId,
    string ScenarioNumber,
    string Name,
    string ScenarioType,
    string Status,
    decimal TotalRevenue,
    decimal ProfitAfterTax,
    decimal ClosingCash,
    decimal ClosingDebt,
    decimal WorkingCapitalRequirement,
    decimal MaxBalanceDifference);

public sealed record FinalAccountsProjectionComparisonMonthDto(
    Guid ScenarioId,
    string ScenarioNumber,
    int MonthNumber,
    DateTime MonthStart,
    decimal Revenue,
    decimal ProfitAfterTax,
    decimal CashBalance,
    decimal BalanceDifference);

public sealed record FinalAccountsProjectionEventDto(DateTime At, string Event, string? Actor, string Detail);

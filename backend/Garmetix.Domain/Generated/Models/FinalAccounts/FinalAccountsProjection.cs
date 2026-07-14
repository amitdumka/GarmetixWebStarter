using System.ComponentModel.DataAnnotations;
using Garmetix.Core.Models.Base;

namespace Garmetix.Core.Models.FinalAccounts;

public enum FinalAccountsProjectionScenarioType
{
    Conservative = 1,
    Base = 2,
    Optimistic = 3,
    Custom = 4
}

public enum FinalAccountsProjectionScenarioStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Archived = 4
}

public enum FinalAccountsProjectionBaselineSource
{
    Manual = 1,
    Actuals = 2
}

public sealed class FinalAccountsProjectionScenario : BaseEntity
{
    [Display(Name = "Company")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store")] public Guid? StoreId { get; set; }
    [Display(Name = "Scenario Number")] public string ScenarioNumber { get; set; } = string.Empty;
    [Display(Name = "Name")] public string Name { get; set; } = string.Empty;
    [Display(Name = "Description")] public string? Description { get; set; }
    [Display(Name = "Scenario Type")] public FinalAccountsProjectionScenarioType ScenarioType { get; set; } = FinalAccountsProjectionScenarioType.Base;
    [Display(Name = "Status")] public FinalAccountsProjectionScenarioStatus Status { get; set; } = FinalAccountsProjectionScenarioStatus.Draft;
    [Display(Name = "Baseline Source")] public FinalAccountsProjectionBaselineSource BaselineSource { get; set; } = FinalAccountsProjectionBaselineSource.Manual;
    [Display(Name = "Baseline From")] public DateTime? BaselineFrom { get; set; }
    [Display(Name = "Baseline To")] public DateTime? BaselineTo { get; set; }
    [Display(Name = "Projection Start")] public DateTime ProjectionStart { get; set; } = DateTime.UtcNow.Date;
    [Display(Name = "Horizon Months")] public int HorizonMonths { get; set; } = 12;
    [Display(Name = "Assumption Version")] public int ActiveAssumptionVersion { get; set; } = 1;
    [Display(Name = "Baseline Revenue")] public decimal BaselineMonthlyRevenue { get; set; }
    [Display(Name = "Baseline Gross Profit")] public decimal BaselineMonthlyGrossProfit { get; set; }
    [Display(Name = "Baseline Profit After Tax")] public decimal BaselineMonthlyProfitAfterTax { get; set; }
    [Display(Name = "Baseline Cash")] public decimal BaselineCash { get; set; }
    [Display(Name = "Baseline Inventory")] public decimal BaselineInventory { get; set; }
    [Display(Name = "Baseline Debtors")] public decimal BaselineDebtors { get; set; }
    [Display(Name = "Baseline Creditors")] public decimal BaselineCreditors { get; set; }
    [Display(Name = "Baseline Fixed Assets")] public decimal BaselineFixedAssets { get; set; }
    [Display(Name = "Baseline Debt")] public decimal BaselineDebt { get; set; }
    [Display(Name = "Baseline Capital")] public decimal BaselineCapital { get; set; }
    [Display(Name = "Submitted At")] public DateTime? SubmittedAt { get; set; }
    [Display(Name = "Submitted By")] public string? SubmittedBy { get; set; }
    [Display(Name = "Approved At")] public DateTime? ApprovedAt { get; set; }
    [Display(Name = "Approved By")] public string? ApprovedBy { get; set; }
    [Display(Name = "Archived At")] public DateTime? ArchivedAt { get; set; }
    [Display(Name = "Archived By")] public string? ArchivedBy { get; set; }
    [Display(Name = "Decision Notes")] public string? DecisionNotes { get; set; }
    [Display(Name = "Revision")] public int Revision { get; set; }
    [Display(Name = "Created By")] public string? CreatedBy { get; set; }
    [Display(Name = "Updated By")] public string? UpdatedBy { get; set; }
}

public sealed class FinalAccountsProjectionAssumptionVersion : BaseEntity
{
    [Display(Name = "Company")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store")] public Guid? StoreId { get; set; }
    [Display(Name = "Projection Scenario")] public Guid ScenarioId { get; set; }
    [Display(Name = "Version")] public int Version { get; set; } = 1;
    [Display(Name = "Active")] public bool IsActive { get; set; } = true;
    [Display(Name = "Revenue Growth Percent")] public decimal RevenueGrowthPercent { get; set; }
    [Display(Name = "Seasonality Factors")] public string SeasonalityFactorsCsv { get; set; } = "1,1,1,1,1,1,1,1,1,1,1,1";
    [Display(Name = "New Store Monthly Revenue")] public decimal NewStoreMonthlyRevenue { get; set; }
    [Display(Name = "Average Bill Value")] public decimal AverageBillValue { get; set; }
    [Display(Name = "Customer Count Growth Percent")] public decimal CustomerCountGrowthPercent { get; set; }
    [Display(Name = "Returns Discount Percent")] public decimal ReturnsDiscountPercent { get; set; }
    [Display(Name = "Gross Margin Percent")] public decimal GrossMarginPercent { get; set; }
    [Display(Name = "Purchase Inflation Percent")] public decimal PurchaseInflationPercent { get; set; }
    [Display(Name = "Inventory Days")] public decimal InventoryDays { get; set; }
    [Display(Name = "Debtor Days")] public decimal DebtorDays { get; set; }
    [Display(Name = "Creditor Days")] public decimal CreditorDays { get; set; }
    [Display(Name = "Employee Cost Monthly")] public decimal EmployeeCostMonthly { get; set; }
    [Display(Name = "Salary Growth Percent")] public decimal SalaryGrowthPercent { get; set; }
    [Display(Name = "Rent Expense Monthly")] public decimal RentExpenseMonthly { get; set; }
    [Display(Name = "Expense Escalation Percent")] public decimal ExpenseEscalationPercent { get; set; }
    [Display(Name = "Capex Monthly")] public decimal CapexMonthly { get; set; }
    [Display(Name = "Depreciation Rate Percent")] public decimal DepreciationRatePercent { get; set; }
    [Display(Name = "Debt Opening")] public decimal DebtOpening { get; set; }
    [Display(Name = "Interest Rate Percent")] public decimal InterestRatePercent { get; set; }
    [Display(Name = "Debt Repayment Monthly")] public decimal DebtRepaymentMonthly { get; set; }
    [Display(Name = "Capital Injection Monthly")] public decimal CapitalInjectionMonthly { get; set; }
    [Display(Name = "Drawings Monthly")] public decimal DrawingsMonthly { get; set; }
    [Display(Name = "Tax Rate Percent")] public decimal TaxRatePercent { get; set; }
    [Display(Name = "Minimum Cash")] public decimal MinimumCash { get; set; }
    [Display(Name = "Notes")] public string? Notes { get; set; }
    [Display(Name = "Created By")] public string? CreatedBy { get; set; }
    [Display(Name = "Revision")] public int Revision { get; set; }
}

public sealed class FinalAccountsProjectionMonth : BaseEntity
{
    [Display(Name = "Projection Scenario")] public Guid ScenarioId { get; set; }
    [Display(Name = "Assumption Version")] public int AssumptionVersion { get; set; }
    [Display(Name = "Month Number")] public int MonthNumber { get; set; }
    [Display(Name = "Month Start")] public DateTime MonthStart { get; set; }
    [Display(Name = "Month End")] public DateTime MonthEnd { get; set; }
    [Display(Name = "Revenue")] public decimal Revenue { get; set; }
    [Display(Name = "Returns And Discounts")] public decimal ReturnsAndDiscounts { get; set; }
    [Display(Name = "Net Revenue")] public decimal NetRevenue { get; set; }
    [Display(Name = "COGS")] public decimal CostOfGoodsSold { get; set; }
    [Display(Name = "Gross Profit")] public decimal GrossProfit { get; set; }
    [Display(Name = "Payroll Expense")] public decimal PayrollExpense { get; set; }
    [Display(Name = "Rent Expense")] public decimal RentExpense { get; set; }
    [Display(Name = "Other Expense")] public decimal OtherExpense { get; set; }
    [Display(Name = "Depreciation")] public decimal Depreciation { get; set; }
    [Display(Name = "Interest")] public decimal Interest { get; set; }
    [Display(Name = "Tax")] public decimal Tax { get; set; }
    [Display(Name = "Profit After Tax")] public decimal ProfitAfterTax { get; set; }
    [Display(Name = "Inventory Balance")] public decimal InventoryBalance { get; set; }
    [Display(Name = "Debtor Balance")] public decimal DebtorBalance { get; set; }
    [Display(Name = "Creditor Balance")] public decimal CreditorBalance { get; set; }
    [Display(Name = "Fixed Assets")] public decimal FixedAssets { get; set; }
    [Display(Name = "Debt Balance")] public decimal DebtBalance { get; set; }
    [Display(Name = "Capital Balance")] public decimal CapitalBalance { get; set; }
    [Display(Name = "Cash Balance")] public decimal CashBalance { get; set; }
    [Display(Name = "Total Assets")] public decimal TotalAssets { get; set; }
    [Display(Name = "Total Liabilities Equity")] public decimal TotalLiabilitiesEquity { get; set; }
    [Display(Name = "Balance Difference")] public decimal BalanceDifference { get; set; }
    [Display(Name = "Operating Cash Flow")] public decimal OperatingCashFlow { get; set; }
    [Display(Name = "Investing Cash Flow")] public decimal InvestingCashFlow { get; set; }
    [Display(Name = "Financing Cash Flow")] public decimal FinancingCashFlow { get; set; }
    [Display(Name = "Closing Cash Flow")] public decimal ClosingCashFlow { get; set; }
    [Display(Name = "Working Capital Requirement")] public decimal WorkingCapitalRequirement { get; set; }
    [Display(Name = "Break Even Revenue")] public decimal BreakEvenRevenue { get; set; }
    [Display(Name = "Current Ratio")] public decimal CurrentRatio { get; set; }
    [Display(Name = "Debt Equity Ratio")] public decimal DebtEquityRatio { get; set; }
    [Display(Name = "Revision")] public int Revision { get; set; }
}

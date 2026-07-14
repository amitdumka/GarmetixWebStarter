using System.ComponentModel.DataAnnotations;
using Garmetix.Core.Models.Base;

namespace Garmetix.Core.Models.FinalAccounts;

public enum FinalAccountsAccountType
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Income = 4,
    Expense = 5,
    ContraAsset = 6,
    ContraLiability = 7
}

public enum FinalAccountsNaturalBalance
{
    Debit = 1,
    Credit = 2
}

public enum FinalAccountsMappingSourceType
{
    Sales = 1,
    Purchase = 2,
    Inventory = 3,
    Gst = 4,
    Payroll = 5,
    CashBank = 6,
    Customer = 7,
    Vendor = 8,
    Adjustment = 9,
    Expense = 10,
    InterStore = 11
}

public enum FinalAccountsPeriodStatus
{
    Draft = 1,
    Open = 2,
    Closed = 3,
    Locked = 4
}

public sealed class FinalAccountsAccountGroup : BaseEntity
{
    [Display(Name = "Company")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store")] public Guid? StoreId { get; set; }
    [Display(Name = "Parent Group")] public Guid? ParentGroupId { get; set; }
    [Display(Name = "Code")] public string Code { get; set; } = string.Empty;
    [Display(Name = "Name")] public string Name { get; set; } = string.Empty;
    [Display(Name = "Account Type")] public FinalAccountsAccountType AccountType { get; set; }
    [Display(Name = "Natural Balance")] public FinalAccountsNaturalBalance NaturalBalance { get; set; }
    [Display(Name = "Sort Order")] public int SortOrder { get; set; }
    [Display(Name = "System Group")] public bool IsSystem { get; set; }
    [Display(Name = "Active")] public bool IsActive { get; set; } = true;
    [Display(Name = "Description")] public string? Description { get; set; }
    [Display(Name = "Revision")] public int Revision { get; set; }
    [Display(Name = "Created By")] public string? CreatedBy { get; set; }
    [Display(Name = "Updated By")] public string? UpdatedBy { get; set; }
}

public sealed class FinalAccountsAccount : BaseEntity
{
    [Display(Name = "Company")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store")] public Guid? StoreId { get; set; }
    [Display(Name = "Group")] public Guid AccountGroupId { get; set; }
    [Display(Name = "Parent Account")] public Guid? ParentAccountId { get; set; }
    [Display(Name = "Code")] public string Code { get; set; } = string.Empty;
    [Display(Name = "Name")] public string Name { get; set; } = string.Empty;
    [Display(Name = "Account Type")] public FinalAccountsAccountType AccountType { get; set; }
    [Display(Name = "Natural Balance")] public FinalAccountsNaturalBalance NaturalBalance { get; set; }
    [Display(Name = "Opening Balance")] public decimal OpeningBalance { get; set; }
    [Display(Name = "Control Account")] public bool IsControlAccount { get; set; }
    [Display(Name = "System Account")] public bool IsSystem { get; set; }
    [Display(Name = "Active")] public bool IsActive { get; set; } = true;
    [Display(Name = "Description")] public string? Description { get; set; }
    [Display(Name = "Sort Order")] public int SortOrder { get; set; }
    [Display(Name = "Revision")] public int Revision { get; set; }
    [Display(Name = "Created By")] public string? CreatedBy { get; set; }
    [Display(Name = "Updated By")] public string? UpdatedBy { get; set; }
}

public sealed class FinalAccountsAccountMapping : BaseEntity
{
    [Display(Name = "Company")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store")] public Guid? StoreId { get; set; }
    [Display(Name = "Source Type")] public FinalAccountsMappingSourceType SourceType { get; set; }
    [Display(Name = "Mapping Key")] public string MappingKey { get; set; } = string.Empty;
    [Display(Name = "Display Name")] public string DisplayName { get; set; } = string.Empty;
    [Display(Name = "Account")] public Guid AccountId { get; set; }
    [Display(Name = "Required")] public bool IsRequired { get; set; } = true;
    [Display(Name = "System Mapping")] public bool IsSystem { get; set; }
    [Display(Name = "Active")] public bool IsActive { get; set; } = true;
    [Display(Name = "Notes")] public string? Notes { get; set; }
    [Display(Name = "Revision")] public int Revision { get; set; }
    [Display(Name = "Created By")] public string? CreatedBy { get; set; }
    [Display(Name = "Updated By")] public string? UpdatedBy { get; set; }
}

public sealed class FinalAccountsFiscalYear : BaseEntity
{
    [Display(Name = "Company")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store")] public Guid? StoreId { get; set; }
    [Display(Name = "Name")] public string Name { get; set; } = string.Empty;
    [Display(Name = "Start Date")] public DateTime StartDate { get; set; }
    [Display(Name = "End Date")] public DateTime EndDate { get; set; }
    [Display(Name = "Status")] public FinalAccountsPeriodStatus Status { get; set; } = FinalAccountsPeriodStatus.Draft;
    [Display(Name = "Closed At")] public DateTime? ClosedAt { get; set; }
    [Display(Name = "Revision")] public int Revision { get; set; }
    [Display(Name = "Created By")] public string? CreatedBy { get; set; }
    [Display(Name = "Updated By")] public string? UpdatedBy { get; set; }
}

public sealed class FinalAccountsFiscalPeriod : BaseEntity
{
    [Display(Name = "Company")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store")] public Guid? StoreId { get; set; }
    [Display(Name = "Fiscal Year")] public Guid FiscalYearId { get; set; }
    [Display(Name = "Period Number")] public int PeriodNumber { get; set; }
    [Display(Name = "Name")] public string Name { get; set; } = string.Empty;
    [Display(Name = "Start Date")] public DateTime StartDate { get; set; }
    [Display(Name = "End Date")] public DateTime EndDate { get; set; }
    [Display(Name = "Status")] public FinalAccountsPeriodStatus Status { get; set; } = FinalAccountsPeriodStatus.Draft;
    [Display(Name = "Closed At")] public DateTime? ClosedAt { get; set; }
    [Display(Name = "Revision")] public int Revision { get; set; }
    [Display(Name = "Created By")] public string? CreatedBy { get; set; }
    [Display(Name = "Updated By")] public string? UpdatedBy { get; set; }
}

using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.Swalekha;

/// <summary>
/// A Fixed Deposit (PersonalFin_08 - Investments I). AccountId optionally links to one of the
/// Owner's own SwalekhaAccount rows - the funding source and, on MarkMatured, the account credited
/// with the maturity payout, mirroring the roll-up pattern PersonalFin_05's Trip-close already
/// established. Purely a tracker until MarkMatured actually moves money.
/// </summary>
public class SwalekhaFixedDeposit : SwalekhaOwnedEntity
{
    public SwalekhaFixedDeposit()
    {
        BankName = string.Empty;
    }

    [Display(Name = "Bank Name")] public string BankName { get; set; }
    [Display(Name = "FD Number")] public string? FdNumber { get; set; }
    [Display(Name = "Account Id")] public Guid? AccountId { get; set; }
    [Display(Name = "Principal Amount")] public decimal PrincipalAmount { get; set; }
    [Display(Name = "Interest Rate Percent")] public decimal InterestRatePercent { get; set; }
    [Display(Name = "Tenure Months")] public int TenureMonths { get; set; }
    [Display(Name = "Start Date")] public DateTime StartDate { get; set; }
    [Display(Name = "Maturity Date")] public DateTime MaturityDate { get; set; }
    [Display(Name = "Maturity Amount")] public decimal? MaturityAmount { get; set; }
    [Display(Name = "Auto Renew")] public bool AutoRenew { get; set; }
    [Display(Name = "TDS Deducted")] public decimal? TdsDeducted { get; set; }
    [Display(Name = "Is Closed")] public bool IsClosed { get; set; }
    [Display(Name = "Closed At")] public DateTime? ClosedAt { get; set; }
    [Display(Name = "Notes")] public string? Notes { get; set; }
}

/// <summary>
/// A Recurring Deposit (PersonalFin_08 - Investments I). InstallmentsPaid is a simple running
/// counter updated by the RecordInstallment action, which - like SwalekhaFixedDeposit's
/// MarkMatured - optionally debits the linked SwalekhaAccount so the RD genuinely integrates with
/// the Accounts Hub ledger instead of being a disconnected tracker.
/// </summary>
public class SwalekhaRecurringDeposit : SwalekhaOwnedEntity
{
    public SwalekhaRecurringDeposit()
    {
        BankName = string.Empty;
    }

    [Display(Name = "Bank Name")] public string BankName { get; set; }
    [Display(Name = "RD Number")] public string? RdNumber { get; set; }
    [Display(Name = "Account Id")] public Guid? AccountId { get; set; }
    [Display(Name = "Monthly Installment")] public decimal MonthlyInstallment { get; set; }
    [Display(Name = "Interest Rate Percent")] public decimal InterestRatePercent { get; set; }
    [Display(Name = "Tenure Months")] public int TenureMonths { get; set; }
    [Display(Name = "Start Date")] public DateTime StartDate { get; set; }
    [Display(Name = "Maturity Date")] public DateTime MaturityDate { get; set; }
    [Display(Name = "Maturity Amount")] public decimal? MaturityAmount { get; set; }
    [Display(Name = "Installments Paid")] public int InstallmentsPaid { get; set; }
    [Display(Name = "Is Closed")] public bool IsClosed { get; set; }
    [Display(Name = "Closed At")] public DateTime? ClosedAt { get; set; }
    [Display(Name = "Notes")] public string? Notes { get; set; }
}

using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.Swalekha;

public enum SwalekhaMutualFundInvestmentMode
{
    Lumpsum,
    SIP
}

public enum SwalekhaMutualFundTransactionType
{
    Purchase,
    SipInstallment,
    Redemption
}

/// <summary>
/// A Mutual Fund folio (PersonalFin_09 - Investments II). CurrentUnits/TotalInvested are running
/// totals kept in lockstep with every SwalekhaMutualFundTransaction, mirroring
/// SwalekhaAccount.CurrentBalance - never recomputed from history on read. AccountId optionally
/// links to one of the Owner's own SwalekhaAccounts: Purchase/SipInstallment debit it,
/// Redemption credits it, matching the money-integration pattern PersonalFin_08 established for
/// FD/RD. CurrentNav is manually updated (no live market-data provider in this stage) and drives
/// the current-value/returns/XIRR calculation.
/// </summary>
public class SwalekhaMutualFund : SwalekhaOwnedEntity
{
    public SwalekhaMutualFund()
    {
        SchemeName = string.Empty;
    }

    [Display(Name = "Scheme Name")] public string SchemeName { get; set; }
    [Display(Name = "AMC")] public string? Amc { get; set; }
    [Display(Name = "Folio Number")] public string? FolioNumber { get; set; }
    [Display(Name = "Account Id")] public Guid? AccountId { get; set; }
    [Display(Name = "Investment Mode")] public SwalekhaMutualFundInvestmentMode InvestmentMode { get; set; }
    [Display(Name = "SIP Amount")] public decimal? SipAmount { get; set; }
    [Display(Name = "SIP Day Of Month")] public int? SipDayOfMonth { get; set; }
    [Display(Name = "Last SIP Installment Date")] public DateTime? LastSipInstallmentDate { get; set; }
    [Display(Name = "Current NAV")] public decimal? CurrentNav { get; set; }
    [Display(Name = "Current NAV Updated At")] public DateTime? CurrentNavUpdatedAt { get; set; }
    [Display(Name = "Current Units")] public decimal CurrentUnits { get; set; }
    [Display(Name = "Total Invested")] public decimal TotalInvested { get; set; }
    [Display(Name = "Is Active")] public bool IsActive { get; set; } = true;
    [Display(Name = "Notes")] public string? Notes { get; set; }
}

/// <summary>
/// One purchase/SIP-installment/redemption event against a SwalekhaMutualFund. Redemption's
/// contribution to TotalInvested is removed at the fund's average cost per unit at the time of
/// redemption (TotalInvested * unitsRedeemed / CurrentUnits before the redemption) - a standard
/// average-cost approximation for a personal tracker, not full FIFO/LIFO lot accounting.
/// </summary>
public class SwalekhaMutualFundTransaction : SwalekhaOwnedEntity
{
    public SwalekhaMutualFundTransaction()
    {
        Narration = string.Empty;
    }

    [Display(Name = "Fund Id")] public Guid FundId { get; set; }
    [Display(Name = "Transaction Type")] public SwalekhaMutualFundTransactionType TransactionType { get; set; }
    [Display(Name = "Transaction Date")] public DateTime TransactionDate { get; set; }
    [Display(Name = "Units")] public decimal Units { get; set; }
    [Display(Name = "NAV At Transaction")] public decimal NavAtTransaction { get; set; }
    [Display(Name = "Amount")] public decimal Amount { get; set; }
    [Display(Name = "Narration")] public string Narration { get; set; }

    /// <summary>
    /// Redemption only: exactly how much this redemption removed from the fund's TotalInvested
    /// at the time (the average-cost-per-unit calculation), recorded so a later delete/reverse can
    /// restore TotalInvested precisely instead of re-deriving a ratio against a CurrentUnits value
    /// that may have changed since.
    /// </summary>
    [Display(Name = "Invested Amount Removed")] public decimal? InvestedAmountRemoved { get; set; }
}

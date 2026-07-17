using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.Swalekha;

public enum SwalekhaLoanType
{
    Personal,
    Home,
    Car,
    Gold,
    Education,
    Other
}

public enum SwalekhaLoanPaymentType
{
    Emi,
    Prepayment
}

/// <summary>
/// A loan taken by the Owner (PersonalFin_11 - Loans). OutstandingPrincipal is a running total
/// kept in lockstep with every SwalekhaLoanPayment, mirroring SwalekhaAccount.CurrentBalance -
/// never recomputed from history on read. AccountId optionally links to one of the Owner's own
/// SwalekhaAccounts: every EMI/Prepayment debits it, matching the money-integration pattern
/// PersonalFin_08/09/10 established for FD/RD/Mutual Funds/Shares. "Loans Given" deliberately has
/// no entity here - the design scoped it to reuse PersonalFin_03's Person Ledger
/// (SwalekhaPersonLedgerEntryType.LoanGiven) rather than duplicating a second ledger.
/// </summary>
public class SwalekhaLoan : SwalekhaOwnedEntity
{
    public SwalekhaLoan()
    {
        LenderName = string.Empty;
    }

    [Display(Name = "Loan Type")] public SwalekhaLoanType LoanType { get; set; }
    [Display(Name = "Lender Name")] public string LenderName { get; set; }
    [Display(Name = "Loan Number")] public string? LoanNumber { get; set; }
    [Display(Name = "Account Id")] public Guid? AccountId { get; set; }
    [Display(Name = "Principal Amount")] public decimal PrincipalAmount { get; set; }
    [Display(Name = "Interest Rate Percent")] public decimal InterestRatePercent { get; set; }
    [Display(Name = "Tenure Months")] public int TenureMonths { get; set; }
    [Display(Name = "EMI Amount")] public decimal EmiAmount { get; set; }
    [Display(Name = "Start Date")] public DateTime StartDate { get; set; }
    [Display(Name = "Outstanding Principal")] public decimal OutstandingPrincipal { get; set; }
    [Display(Name = "Is Closed")] public bool IsClosed { get; set; }
    [Display(Name = "Closed At")] public DateTime? ClosedAt { get; set; }
    [Display(Name = "Notes")] public string? Notes { get; set; }
}

/// <summary>
/// One EMI or prepayment against a SwalekhaLoan. PrincipalComponent/InterestComponent are stored
/// explicitly (not re-derived) so a later delete can reverse the payment's exact effect on
/// OutstandingPrincipal and the linked account balance.
/// </summary>
public class SwalekhaLoanPayment : SwalekhaOwnedEntity
{
    public SwalekhaLoanPayment()
    {
        Narration = string.Empty;
    }

    [Display(Name = "Loan Id")] public Guid LoanId { get; set; }
    [Display(Name = "Payment Type")] public SwalekhaLoanPaymentType PaymentType { get; set; }
    [Display(Name = "Payment Date")] public DateTime PaymentDate { get; set; }
    [Display(Name = "Amount")] public decimal Amount { get; set; }
    [Display(Name = "Principal Component")] public decimal PrincipalComponent { get; set; }
    [Display(Name = "Interest Component")] public decimal InterestComponent { get; set; }
    [Display(Name = "Outstanding After")] public decimal OutstandingAfter { get; set; }
    [Display(Name = "Narration")] public string Narration { get; set; }
}

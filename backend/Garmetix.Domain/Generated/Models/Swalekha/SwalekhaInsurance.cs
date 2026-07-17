using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.Swalekha;

public enum SwalekhaInsurancePolicyType
{
    Life,
    Health,
    Term,
    Vehicle,
    Property,
    ULIP,
    Other
}

public enum SwalekhaPremiumFrequency
{
    Monthly,
    Quarterly,
    HalfYearly,
    Yearly
}

/// <summary>
/// An insurance policy (PersonalFin_12 - Insurance). AccountId optionally links to one of the
/// Owner's own SwalekhaAccounts: PayPremium debits it per premium, MarkMatured credits it with the
/// maturity payout - the same money-integration pattern PersonalFin_08 established for FD/RD.
/// NextPremiumDueDate/PremiumDueNow are computed at read time from LastPremiumPaidDate (or
/// StartDate if never paid) plus PremiumFrequency, not stored - matches SwalekhaRecurringBill's
/// DueThisMonth pattern from PersonalFin_04, generalized to a frequency instead of a fixed month.
/// </summary>
public class SwalekhaInsurancePolicy : SwalekhaOwnedEntity
{
    public SwalekhaInsurancePolicy()
    {
        Insurer = string.Empty;
    }

    [Display(Name = "Policy Type")] public SwalekhaInsurancePolicyType PolicyType { get; set; }
    [Display(Name = "Insurer")] public string Insurer { get; set; }
    [Display(Name = "Policy Number")] public string? PolicyNumber { get; set; }
    [Display(Name = "Account Id")] public Guid? AccountId { get; set; }
    [Display(Name = "Sum Assured")] public decimal? SumAssured { get; set; }
    [Display(Name = "Premium Amount")] public decimal PremiumAmount { get; set; }
    [Display(Name = "Premium Frequency")] public SwalekhaPremiumFrequency PremiumFrequency { get; set; }
    [Display(Name = "Start Date")] public DateTime StartDate { get; set; }
    [Display(Name = "Nominee Name")] public string? NomineeName { get; set; }
    [Display(Name = "Nominee Relationship")] public string? NomineeRelationship { get; set; }
    [Display(Name = "Maturity Date")] public DateTime? MaturityDate { get; set; }
    [Display(Name = "Maturity Amount")] public decimal? MaturityAmount { get; set; }
    [Display(Name = "Last Premium Paid Date")] public DateTime? LastPremiumPaidDate { get; set; }
    [Display(Name = "Is Matured")] public bool IsMatured { get; set; }
    [Display(Name = "Matured At")] public DateTime? MaturedAt { get; set; }
    [Display(Name = "Is Active")] public bool IsActive { get; set; } = true;
    [Display(Name = "Notes")] public string? Notes { get; set; }
}

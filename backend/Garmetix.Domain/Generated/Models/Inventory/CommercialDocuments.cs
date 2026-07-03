using Garmetix.Core.Enums;
using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.Inventory;

public class CommercialNote : StoreBase
{
    [Display(Name = "Note Number")] public required string NoteNumber { get; set; } = string.Empty;
    [Display(Name = "Note Type")] public NoteType NoteType { get; set; } = NoteType.CreditNote;
    [Display(Name = "Date")] public DateTime OnDate { get; set; } = DateTime.Now;
    [Display(Name = "Party Type")] public PartyType PartyType { get; set; } = PartyType.Customer;
    [Display(Name = "Party")] public Guid? PartyId { get; set; }
    [Display(Name = "Customer")] public Guid? CustomerId { get; set; }
    [Display(Name = "Vendor")] public Guid? VendorId { get; set; }
    [Display(Name = "Party Name")] public required string PartyName { get; set; } = string.Empty;
    [Display(Name = "Party GSTIN")] public string? PartyGstin { get; set; }
    [Display(Name = "Source Type")] public string SourceType { get; set; } = "Manual";
    [Display(Name = "Source Id")] public Guid? SourceId { get; set; }
    [Display(Name = "Source Number")] public string? SourceNumber { get; set; }
    [Display(Name = "Reason")] public string Reason { get; set; } = string.Empty;
    [Display(Name = "Taxable Amount")] public decimal TaxableAmount { get; set; }
    [Display(Name = "Tax Amount")] public decimal TaxAmount { get; set; }
    [Display(Name = "Total Amount")] public decimal Amount { get; set; }
    [Display(Name = "Adjusted")] public bool IsAdjusted { get; set; } = false;
    [Display(Name = "Adjusted Amount")] public decimal AdjustedAmount { get; set; } = 0;
    [Display(Name = "Printed")] public bool Printed { get; set; } = false;
    [Display(Name = "Remarks")] public string? Remarks { get; set; }
}

public class CustomerAdvanceReceipt : StoreBase
{
    [Display(Name = "Receipt Number")] public required string ReceiptNumber { get; set; } = string.Empty;
    [Display(Name = "Date")] public DateTime OnDate { get; set; } = DateTime.Now;
    [Display(Name = "Customer")] public Guid CustomerId { get; set; }
    [Display(Name = "Customer Name")] public required string CustomerName { get; set; } = string.Empty;
    [Display(Name = "Customer Mobile")] public string? CustomerMobileNumber { get; set; }
    [Display(Name = "Amount")] public decimal Amount { get; set; }
    [Display(Name = "Adjusted Amount")] public decimal AdjustedAmount { get; set; } = 0;
    [Display(Name = "Available Amount")] public decimal AvailableAmount { get; set; }
    [Display(Name = "Payment Mode")] public PaymentMode PaymentMode { get; set; } = PaymentMode.Cash;
    [Display(Name = "Bank Account")] public Guid? BankAccountId { get; set; }
    [Display(Name = "Reference Number")] public string? ReferenceNumber { get; set; }
    [Display(Name = "Remarks")] public string? Remarks { get; set; }
}

public class LoyaltyProgram : StoreBase
{
    [Display(Name = "Enabled")] public bool Enabled { get; set; } = true;
    [Display(Name = "Program Name")] public string Name { get; set; } = "Garmetix Loyalty";
    [Display(Name = "Earn Rate Per Rs")] public decimal EarnPointsPerRupee { get; set; } = 0.01m;
    [Display(Name = "Redeem Value Per Point")] public decimal RedeemValuePerPoint { get; set; } = 1m;
    [Display(Name = "Minimum Bill Amount")] public decimal MinimumBillAmount { get; set; } = 0m;
    [Display(Name = "Expiry Days")] public int? ExpiryDays { get; set; }
}

public class LoyaltyPointLedger : StoreBase
{
    [Display(Name = "Customer")] public Guid CustomerId { get; set; }
    [Display(Name = "Customer Name")] public string CustomerName { get; set; } = string.Empty;
    [Display(Name = "Date")] public DateTime OnDate { get; set; } = DateTime.Now;
    [Display(Name = "Source Type")] public string SourceType { get; set; } = string.Empty;
    [Display(Name = "Source Id")] public Guid? SourceId { get; set; }
    [Display(Name = "Source Number")] public string? SourceNumber { get; set; }
    [Display(Name = "Points In")] public decimal PointsIn { get; set; }
    [Display(Name = "Points Out")] public decimal PointsOut { get; set; }
    [Display(Name = "Balance After")] public decimal BalanceAfter { get; set; }
    [Display(Name = "Remarks")] public string? Remarks { get; set; }
}

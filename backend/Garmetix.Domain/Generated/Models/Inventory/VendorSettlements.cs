using Garmetix.Core.Enums;
using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Garmetix.Core.Models.Inventory;

public class VendorSettlement : StoreBase
{
    [Display(Name = "Settlement Number")] public required string SettlementNumber { get; set; } = string.Empty;
    [Display(Name = "Date")] public DateTime OnDate { get; set; } = DateTime.Now;
    [Display(Name = "Vendor", AutoGenerateField = false)] public Guid VendorId { get; set; }
    [Display(Name = "Vendor Name")] public required string VendorName { get; set; } = string.Empty;
    [Display(Name = "Purchase Return", AutoGenerateField = false)] public Guid PurchaseReturnId { get; set; }
    [Display(Name = "Return Number")] public required string ReturnNumber { get; set; } = string.Empty;
    [Display(Name = "Debit Note", AutoGenerateField = false)] public Guid DebitNoteId { get; set; }
    [Display(Name = "Debit Note Number")] public required string DebitNoteNumber { get; set; } = string.Empty;
    [Display(Name = "Settlement Type")] public string SettlementType { get; set; } = "Adjustment";
    [Display(Name = "Adjusted Amount")] public decimal AdjustedAmount { get; set; }
    [Display(Name = "Refund Amount")] public decimal RefundAmount { get; set; }
    [Display(Name = "Total Amount")] public decimal TotalAmount { get; set; }
    [Display(Name = "Payment Mode")] public PaymentMode? PaymentMode { get; set; }
    [Display(Name = "Bank Account", AutoGenerateField = false)] public Guid? BankAccountId { get; set; }
    [Display(Name = "Reference Number")] public string? ReferenceNumber { get; set; }
    [Display(Name = "Voucher", AutoGenerateField = false)] public Guid? VoucherId { get; set; }
    [Display(Name = "Journal Entry", AutoGenerateField = false)] public Guid? JournalEntryId { get; set; }
    [Display(Name = "Bank Transaction", AutoGenerateField = false)] public Guid? BankTransactionId { get; set; }
    [Display(Name = "Status")] public string Status { get; set; } = "Posted";
    [Display(Name = "Remarks")] public string? Remarks { get; set; }

    [JsonIgnore] public virtual PurchaseReturn? PurchaseReturn { get; set; }
    [JsonIgnore] public virtual ICollection<VendorSettlementAllocation> Allocations { get; set; } = new List<VendorSettlementAllocation>();
}

public class VendorSettlementAllocation : CompanyBase
{
    [Display(Name = "Vendor Settlement", AutoGenerateField = false)] public Guid VendorSettlementId { get; set; }
    [Display(Name = "Purchase Invoice", AutoGenerateField = false)] public Guid PurchaseInvoiceId { get; set; }
    [Display(Name = "Purchase Invoice Number")] public required string PurchaseInvoiceNumber { get; set; } = string.Empty;
    [Display(Name = "Allocated Amount")] public decimal Amount { get; set; }

    [JsonIgnore] public virtual VendorSettlement? VendorSettlement { get; set; }
    [JsonIgnore] public virtual PurchaseInvoice? PurchaseInvoice { get; set; }
}

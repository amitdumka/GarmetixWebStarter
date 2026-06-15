using Garmetix.Core.Enums;
using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Garmetix.Core.Models.Inventory;

public class PurchaseReturn : StoreBase
{
    [Display(Name = "Return Number")] public required string ReturnNumber { get; set; } = string.Empty;
    [Display(Name = "Return Date")] public DateTime OnDate { get; set; } = DateTime.Now;
    [Display(Name = "Purchase Invoice", AutoGenerateField = false)] public Guid PurchaseInvoiceId { get; set; }
    [Display(Name = "Original Invoice Number")] public required string OriginalInvoiceNumber { get; set; } = string.Empty;
    [Display(Name = "Original Invoice Date")] public DateTime OriginalInvoiceDate { get; set; }
    [Display(Name = "Supplier Invoice Date")] public DateTime? SupplierInvoiceDate { get; set; }
    [Display(Name = "Vendor", AutoGenerateField = false)] public Guid VendorId { get; set; }
    [Display(Name = "Vendor Name")] public required string VendorName { get; set; } = string.Empty;
    [Display(Name = "Vendor GSTIN")] public string? VendorGstin { get; set; }
    [Display(Name = "Return Kind")] public string ReturnKind { get; set; } = "Partial";
    [Display(Name = "Status")] public string Status { get; set; } = "Posted";
    [Display(Name = "Reason")] public string Reason { get; set; } = string.Empty;
    [Display(Name = "Quantity")] public decimal Quantity { get; set; }
    [Display(Name = "Taxable Amount")] public decimal TaxableAmount { get; set; }
    [Display(Name = "Tax Amount")] public decimal TaxAmount { get; set; }
    [Display(Name = "CGST Amount")] public decimal CGSTAmount { get; set; }
    [Display(Name = "SGST Amount")] public decimal SGSTAmount { get; set; }
    [Display(Name = "IGST Amount")] public decimal IGSTAmount { get; set; }
    [Display(Name = "Return Amount")] public decimal ReturnAmount { get; set; }
    [Display(Name = "Debit Note", AutoGenerateField = false)] public Guid? DebitNoteId { get; set; }
    [Display(Name = "Debit Note Number")] public string? DebitNoteNumber { get; set; }
    [Display(Name = "Item Count")] public int ItemCount { get; set; }
    [Display(Name = "Printed")] public bool Printed { get; set; }
    [Display(Name = "Print Count")] public int PrintCount { get; set; }
    [Display(Name = "Last Printed At")] public DateTime? LastPrintedAt { get; set; }

    [JsonIgnore]
    public virtual PurchaseInvoice? PurchaseInvoice { get; set; }

    [JsonIgnore]
    public virtual ICollection<PurchaseReturnItem> Items { get; set; } = new List<PurchaseReturnItem>();
}

public class PurchaseReturnItem : CompanyBase
{
    [Display(Name = "Purchase Return", AutoGenerateField = false)] public Guid PurchaseReturnId { get; set; }
    [Display(Name = "Purchase Invoice", AutoGenerateField = false)] public Guid PurchaseInvoiceId { get; set; }
    [Display(Name = "Purchase Invoice Item", AutoGenerateField = false)] public Guid PurchaseInvoiceItemId { get; set; }
    [Display(Name = "Product", AutoGenerateField = false)] public Guid ProductId { get; set; }
    [Display(Name = "Product Name")] public required string ProductName { get; set; } = string.Empty;
    [Display(Name = "Barcode")] public required string Barcode { get; set; } = string.Empty;
    [Display(Name = "HSN Code")] public string? HSNCode { get; set; }
    [Display(Name = "Unit")] public Unit? Unit { get; set; }
    [Display(Name = "Product Category", AutoGenerateField = false)] public Guid? ProductCategoryId { get; set; }
    [Display(Name = "Product Sub Category", AutoGenerateField = false)] public Guid? ProductSubCategoryId { get; set; }
    [Display(Name = "Purchased Quantity")] public decimal PurchasedQuantity { get; set; }
    [Display(Name = "Previously Returned Quantity")] public decimal PreviouslyReturnedQuantity { get; set; }
    [Display(Name = "Returned Quantity")] public decimal ReturnedQuantity { get; set; }
    [Display(Name = "MRP")] public decimal MRP { get; set; }
    [Display(Name = "Unit Rate")] public decimal UnitRate { get; set; }
    [Display(Name = "Discount Amount")] public decimal DiscountAmount { get; set; }
    [Display(Name = "Taxable Amount")] public decimal TaxableAmount { get; set; }
    [Display(Name = "Tax Rate")] public decimal TaxRate { get; set; }
    [Display(Name = "Tax Amount")] public decimal TaxAmount { get; set; }
    [Display(Name = "CGST Amount")] public decimal CGSTAmount { get; set; }
    [Display(Name = "SGST Amount")] public decimal SGSTAmount { get; set; }
    [Display(Name = "IGST Amount")] public decimal IGSTAmount { get; set; }
    [Display(Name = "Return Amount")] public decimal ReturnAmount { get; set; }
    [Display(Name = "Reason")] public string? Reason { get; set; }

    [JsonIgnore]
    public virtual PurchaseReturn? PurchaseReturn { get; set; }
}

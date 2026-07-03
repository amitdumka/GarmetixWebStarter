using Garmetix.Core.Enums;
using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Garmetix.Core.Models.Inventory;

public class PurchaseInvoiceImportBatch : StoreBase
{
    [Display(Name = "Status")] public string Status { get; set; } = "Uploaded";
    [Display(Name = "Source File Name")] public string SourceFileName { get; set; } = string.Empty;
    [Display(Name = "Stored File Path")] public string StoredFilePath { get; set; } = string.Empty;
    [Display(Name = "Content Type")] public string ContentType { get; set; } = string.Empty;
    [Display(Name = "File Size Bytes")] public long FileSizeBytes { get; set; }
    [Display(Name = "SHA-256 Hash")] public string Sha256Hash { get; set; } = string.Empty;
    [Display(Name = "OCR Provider")] public string OcrProvider { get; set; } = "BuiltInTextExtractor";
    [Display(Name = "OCR Status")] public string OcrStatus { get; set; } = "Pending";
    [Display(Name = "Raw Text Path")] public string? RawTextPath { get; set; }
    [Display(Name = "Raw JSON Path")] public string? RawJsonPath { get; set; }
    [Display(Name = "Confidence Score")] public decimal ConfidenceScore { get; set; }
    [Display(Name = "Parser Template")] public string? ParserTemplate { get; set; }
    [Display(Name = "Parser Template Reason")] public string? ParserTemplateReason { get; set; }
    [Display(Name = "Import QA Notes")] public string? ImportQaNotes { get; set; }
    [Display(Name = "Acceptance Status")] public string? AcceptanceStatus { get; set; }
    [Display(Name = "Acceptance Notes")] public string? AcceptanceNotes { get; set; }
    [Display(Name = "Acceptance Tested At")] public DateTime? AcceptanceTestedAt { get; set; }
    [Display(Name = "Acceptance Tested By")] public string? AcceptanceTestedBy { get; set; }
    [Display(Name = "Correction Status")] public string? CorrectionStatus { get; set; }
    [Display(Name = "Correction Notes")] public string? CorrectionNotes { get; set; }
    [Display(Name = "Correction Requested At")] public DateTime? CorrectionRequestedAt { get; set; }
    [Display(Name = "Correction Requested By")] public string? CorrectionRequestedBy { get; set; }

    [Display(Name = "Vendor", AutoGenerateField = false)] public Guid? VendorId { get; set; }
    [Display(Name = "Vendor Name Raw")] public string? VendorNameRaw { get; set; }
    [Display(Name = "Vendor Name Final")] public string? VendorNameFinal { get; set; }
    [Display(Name = "Vendor GSTIN Raw")] public string? VendorGstinRaw { get; set; }
    [Display(Name = "Vendor GSTIN Final")] public string? VendorGstinFinal { get; set; }
    [Display(Name = "Vendor Mobile")] public string? VendorMobileNumber { get; set; }
    [Display(Name = "Vendor Address")] public string? VendorAddress { get; set; }

    [Display(Name = "Supplier Invoice Number")] public string? SupplierInvoiceNumber { get; set; }
    [Display(Name = "Supplier Invoice Date")] public DateTime? SupplierInvoiceDate { get; set; }
    [Display(Name = "Due Date")] public DateTime? DueDate { get; set; }
    [Display(Name = "Taxable Amount")] public decimal TaxableAmount { get; set; }
    [Display(Name = "CGST Amount")] public decimal CgstAmount { get; set; }
    [Display(Name = "SGST Amount")] public decimal SgstAmount { get; set; }
    [Display(Name = "IGST Amount")] public decimal IgstAmount { get; set; }
    [Display(Name = "Freight Amount")] public decimal FreightAmount { get; set; }
    [Display(Name = "Discount Amount")] public decimal DiscountAmount { get; set; }
    [Display(Name = "Round Off")] public decimal RoundOff { get; set; }
    [Display(Name = "Bill Amount")] public decimal BillAmount { get; set; }
    [Display(Name = "Paid Amount")] public decimal PaidAmount { get; set; }
    [Display(Name = "Payment Mode")] public PaymentMode PaymentMode { get; set; } = PaymentMode.Cash;
    [Display(Name = "Bank Account", AutoGenerateField = false)] public Guid? BankAccountId { get; set; }

    [Display(Name = "Duplicate Purchase Invoice", AutoGenerateField = false)] public Guid? DuplicatePurchaseInvoiceId { get; set; }
    [Display(Name = "Posted Purchase Invoice", AutoGenerateField = false)] public Guid? PostedPurchaseInvoiceId { get; set; }
    [Display(Name = "Error Message")] public string? ErrorMessage { get; set; }
    [Display(Name = "Posted At")] public DateTime? PostedAt { get; set; }
    [Display(Name = "Rejected At")] public DateTime? RejectedAt { get; set; }
    [Display(Name = "Verified By")] public string? VerifiedBy { get; set; }
    [Display(Name = "Duplicate Override Reason")] public string? DuplicateOverrideReason { get; set; }
    [Display(Name = "Duplicate Override By")] public string? DuplicateOverrideBy { get; set; }
    [Display(Name = "Duplicate Override At")] public DateTime? DuplicateOverrideAt { get; set; }

    [JsonIgnore] public virtual ICollection<PurchaseInvoiceImportLine>? Lines { get; set; }
    [JsonIgnore] public virtual ICollection<PurchaseInvoiceImportFile>? Files { get; set; }
}

public class PurchaseInvoiceImportLine : StoreBase
{
    [Display(Name = "Batch", AutoGenerateField = false)] public Guid BatchId { get; set; }
    [Display(Name = "Line Number")] public int LineNumber { get; set; }
    [Display(Name = "Product", AutoGenerateField = false)] public Guid? ProductId { get; set; }
    [Display(Name = "Product Name Raw")] public string? ProductNameRaw { get; set; }
    [Display(Name = "Product Name Final")] public string? ProductNameFinal { get; set; }
    [Display(Name = "Barcode Raw")] public string? BarcodeRaw { get; set; }
    [Display(Name = "Barcode Final")] public string? BarcodeFinal { get; set; }
    [Display(Name = "HSN Code")] public string? HsnCode { get; set; }
    [Display(Name = "Unit")] public Unit Unit { get; set; } = Unit.Pcs;
    [Display(Name = "Quantity")] public decimal Quantity { get; set; }
    [Display(Name = "MRP")] public decimal Mrp { get; set; }
    [Display(Name = "Cost Price")] public decimal CostPrice { get; set; }
    [Display(Name = "Unit Discount")] public decimal UnitDiscount { get; set; }
    [Display(Name = "Line Discount")] public decimal LineDiscount { get; set; }
    [Display(Name = "Tax Rate")] public decimal TaxRate { get; set; }
    [Display(Name = "GST Price Mode")] public string GstPriceMode { get; set; } = "Inclusive";
    [Display(Name = "Tax", AutoGenerateField = false)] public Guid? TaxId { get; set; }
    [Display(Name = "Taxable Amount")] public decimal TaxableAmount { get; set; }
    [Display(Name = "Tax Amount")] public decimal TaxAmount { get; set; }
    [Display(Name = "CGST Amount")] public decimal CgstAmount { get; set; }
    [Display(Name = "SGST Amount")] public decimal SgstAmount { get; set; }
    [Display(Name = "IGST Amount")] public decimal IgstAmount { get; set; }
    [Display(Name = "Line Total")] public decimal LineTotal { get; set; }
    [Display(Name = "Confidence Score")] public decimal ConfidenceScore { get; set; }
    [Display(Name = "Match Status")] public string MatchStatus { get; set; } = "NeedsManualReview";
    [Display(Name = "Review Required")] public bool ReviewRequired { get; set; } = true;
    [Display(Name = "Review Message")] public string? ReviewMessage { get; set; }
    [Display(Name = "Product Category", AutoGenerateField = false)] public Guid? ProductCategoryId { get; set; }
    [Display(Name = "Product Sub Category", AutoGenerateField = false)] public Guid? ProductSubCategoryId { get; set; }
    [Display(Name = "Product Type")] public ProductType ProductType { get; set; } = ProductType.Apparels;
    [Display(Name = "Product Group")] public ProductGroup ProductGroup { get; set; } = ProductGroup.Shirting;
    [Display(Name = "Ignored")] public bool Ignored { get; set; }

    [JsonIgnore] public virtual PurchaseInvoiceImportBatch? Batch { get; set; }
}


public class PurchaseInvoiceImportVendorProfile : StoreBase
{
    [Display(Name = "Vendor", AutoGenerateField = false)] public Guid? VendorId { get; set; }
    [Display(Name = "Vendor GSTIN")] public string? VendorGstin { get; set; }
    [Display(Name = "Vendor Name")] public string? VendorName { get; set; }
    [Display(Name = "Ignored Line Patterns JSON")] public string? IgnoredLinePatternsJson { get; set; }
    [Display(Name = "Product Aliases JSON")] public string? ProductAliasesJson { get; set; }
    [Display(Name = "Last Learned At")] public DateTime? LastLearnedAt { get; set; }
    [Display(Name = "Successful Draft Count")] public int SuccessfulDraftCount { get; set; }
    [Display(Name = "Learning Notes")] public string? LearningNotes { get; set; }
    [Display(Name = "Preferred Parser Template")] public string? PreferredParserTemplate { get; set; }
}

public class PurchaseInvoiceImportFile : StoreBase
{
    [Display(Name = "Batch", AutoGenerateField = false)] public Guid BatchId { get; set; }
    [Display(Name = "File Kind")] public string FileKind { get; set; } = "OriginalUpload";
    [Display(Name = "Original File Name")] public string OriginalFileName { get; set; } = string.Empty;
    [Display(Name = "Stored File Path")] public string StoredFilePath { get; set; } = string.Empty;
    [Display(Name = "Content Type")] public string ContentType { get; set; } = string.Empty;
    [Display(Name = "File Size Bytes")] public long FileSizeBytes { get; set; }
    [Display(Name = "SHA-256 Hash")] public string Sha256Hash { get; set; } = string.Empty;

    [JsonIgnore] public virtual PurchaseInvoiceImportBatch? Batch { get; set; }
}

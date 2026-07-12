using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.GstTax;

/// <summary>
/// A configured GST API provider (GSTIN/HSN/e-invoice/e-way bill). Scoping is nullable by design:
/// null StoreId/StoreGroupId/CompanyId means the provider applies globally, letting resolution fall
/// back Store -> Company -> Global -> LocalMasterOnly (see GstProviderRegistry).
/// </summary>
public class GstApiProvider : BaseEntity
{
    public GstApiProvider()
    {
        ProviderName = string.Empty;
        ProviderType = "LocalMasterOnly";
        Environment = "Sandbox";
    }

    [Display(Name = "Company Id")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group Id")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store Id")] public Guid? StoreId { get; set; }
    [Display(Name = "Provider Name")] public string ProviderName { get; set; }
    [Display(Name = "Provider Type")] public string ProviderType { get; set; }
    [Display(Name = "Environment")] public string Environment { get; set; }
    [Display(Name = "Base Url")] public string? BaseUrl { get; set; }
    [Display(Name = "Auth Url")] public string? AuthUrl { get; set; }
    [Display(Name = "Is Enabled")] public bool IsEnabled { get; set; } = true;
    [Display(Name = "Priority")] public int Priority { get; set; } = 100;
    [Display(Name = "Fallback Enabled")] public bool FallbackEnabled { get; set; } = true;
    [Display(Name = "Timeout Seconds")] public int TimeoutSeconds { get; set; } = 30;
    [Display(Name = "Max Retries")] public int MaxRetries { get; set; } = 1;
    [Display(Name = "Notes")] public string? Notes { get; set; }
    [Display(Name = "Created By")] public string? CreatedBy { get; set; }
    [Display(Name = "Updated By")] public string? UpdatedBy { get; set; }
}

/// <summary>Feature flags a provider supports (GSTIN_LOOKUP, HSN_LOOKUP, etc.), each with its own fallback priority.</summary>
public class GstApiProviderFeature : BaseEntity
{
    public GstApiProviderFeature()
    {
        FeatureCode = string.Empty;
    }

    [Display(Name = "Provider Id")] public Guid ProviderId { get; set; }
    [Display(Name = "Feature Code")] public string FeatureCode { get; set; }
    [Display(Name = "Is Enabled")] public bool IsEnabled { get; set; } = true;
    [Display(Name = "Priority")] public int Priority { get; set; } = 100;
}

/// <summary>
/// Encrypted provider secrets. EncryptedValue always holds ciphertext (via GstCredentialProtector) -
/// the decrypted value is never returned to the frontend, only used server-side during provider calls.
/// </summary>
public class GstApiProviderCredential : BaseEntity
{
    public GstApiProviderCredential()
    {
        CredentialKey = string.Empty;
        EncryptedValue = string.Empty;
    }

    [Display(Name = "Provider Id")] public Guid ProviderId { get; set; }
    [Display(Name = "Credential Key")] public string CredentialKey { get; set; }
    [Display(Name = "Encrypted Value")] public string EncryptedValue { get; set; }
    [Display(Name = "Masked Display Value")] public string? MaskedDisplayValue { get; set; }
}

/// <summary>Full audit trail of every external GST API call. Secrets/headers must be redacted before writing here.</summary>
public class GstApiCallLog : BaseEntity
{
    public GstApiCallLog()
    {
        FeatureCode = string.Empty;
    }

    [Display(Name = "Provider Id")] public Guid? ProviderId { get; set; }
    [Display(Name = "Feature Code")] public string FeatureCode { get; set; }
    [Display(Name = "Request Method")] public string? RequestMethod { get; set; }
    [Display(Name = "Request Url")] public string? RequestUrl { get; set; }
    [Display(Name = "Request Hash")] public string? RequestHash { get; set; }
    [Display(Name = "Request Payload JSON")] public string? RequestPayloadJson { get; set; }
    [Display(Name = "Response Status Code")] public int? ResponseStatusCode { get; set; }
    [Display(Name = "Response Payload JSON")] public string? ResponsePayloadJson { get; set; }
    [Display(Name = "Is Success")] public bool IsSuccess { get; set; }
    [Display(Name = "Error Code")] public string? ErrorCode { get; set; }
    [Display(Name = "Error Message")] public string? ErrorMessage { get; set; }
    [Display(Name = "Duration Ms")] public int? DurationMs { get; set; }
    [Display(Name = "Invoice Id")] public Guid? InvoiceId { get; set; }
    [Display(Name = "Purchase Invoice Id")] public Guid? PurchaseInvoiceId { get; set; }
    [Display(Name = "Customer Id")] public Guid? CustomerId { get; set; }
    [Display(Name = "Vendor Id")] public Guid? VendorId { get; set; }
    [Display(Name = "Product Id")] public Guid? ProductId { get; set; }
    [Display(Name = "Gstin")] public string? Gstin { get; set; }
    [Display(Name = "Hsn Code")] public string? HsnCode { get; set; }
    [Display(Name = "Company Id")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Id")] public Guid? StoreId { get; set; }
    [Display(Name = "Created By")] public string? CreatedBy { get; set; }
}

/// <summary>Cached verified-GSTIN details, shared across companies since GSTIN registration data is national, not tenant-specific.</summary>
public class GstinVerificationCache : BaseEntity
{
    public GstinVerificationCache()
    {
        Gstin = string.Empty;
    }

    [Display(Name = "Gstin")] public string Gstin { get; set; }
    [Display(Name = "Legal Name")] public string? LegalName { get; set; }
    [Display(Name = "Trade Name")] public string? TradeName { get; set; }
    [Display(Name = "Taxpayer Type")] public string? TaxpayerType { get; set; }
    [Display(Name = "Registration Status")] public string? RegistrationStatus { get; set; }
    [Display(Name = "Registration Date")] public DateTime? RegistrationDate { get; set; }
    [Display(Name = "Cancellation Date")] public DateTime? CancellationDate { get; set; }
    [Display(Name = "State Code")] public string? StateCode { get; set; }
    [Display(Name = "State Name")] public string? StateName { get; set; }
    [Display(Name = "Principal Address")] public string? PrincipalAddress { get; set; }
    [Display(Name = "Additional Address JSON")] public string AdditionalAddressJson { get; set; } = "[]";
    [Display(Name = "Nature Of Business JSON")] public string NatureOfBusinessJson { get; set; } = "[]";
    [Display(Name = "Last Verified At")] public DateTime? LastVerifiedAt { get; set; }
    [Display(Name = "Verification Source")] public string? VerificationSource { get; set; }
    [Display(Name = "Raw Response JSON")] public string? RawResponseJson { get; set; }
    [Display(Name = "Is Active")] public bool? IsActive { get; set; }
}

/// <summary>Local HSN/SAC directory - the first-class, API-independent source used for daily billing.</summary>
public class GstHsnMaster : BaseEntity
{
    public GstHsnMaster()
    {
        HsnCode = string.Empty;
        CodeType = "Goods";
    }

    [Display(Name = "Hsn Code")] public string HsnCode { get; set; }
    [Display(Name = "Code Type")] public string CodeType { get; set; }
    [Display(Name = "Chapter Code")] public string? ChapterCode { get; set; }
    [Display(Name = "Description")] public string? Description { get; set; }
    [Display(Name = "Technical Description")] public string? TechnicalDescription { get; set; }
    [Display(Name = "Common Trade Description")] public string? CommonTradeDescription { get; set; }
    [Display(Name = "Related Codes JSON")] public string RelatedCodesJson { get; set; } = "[]";
    [Display(Name = "Default Uqc")] public string? DefaultUqc { get; set; }
    [Display(Name = "Default Gst Rate")] public decimal? DefaultGstRate { get; set; }
    [Display(Name = "Cgst Rate")] public decimal? CgstRate { get; set; }
    [Display(Name = "Sgst Rate")] public decimal? SgstRate { get; set; }
    [Display(Name = "Igst Rate")] public decimal? IgstRate { get; set; }
    [Display(Name = "Cess Rate")] public decimal? CessRate { get; set; }
    [Display(Name = "Effective From")] public DateTime? EffectiveFrom { get; set; }
    [Display(Name = "Effective To")] public DateTime? EffectiveTo { get; set; }
    [Display(Name = "Source")] public string? Source { get; set; }
    [Display(Name = "Is Active")] public bool IsActive { get; set; } = true;
}

/// <summary>Date-effective GST rate rules by HSN/category/price-threshold - includes the garment 5%/18% threshold rule.</summary>
public class GstTaxRateRule : BaseEntity
{
    public GstTaxRateRule()
    {
        RuleName = string.Empty;
        GoodsOrService = "Goods";
    }

    [Display(Name = "Rule Name")] public string RuleName { get; set; }
    [Display(Name = "Hsn Code")] public string? HsnCode { get; set; }
    [Display(Name = "Product Category")] public string? ProductCategory { get; set; }
    [Display(Name = "Goods Or Service")] public string GoodsOrService { get; set; }
    [Display(Name = "Tax Rate")] public decimal TaxRate { get; set; }
    [Display(Name = "Cgst Rate")] public decimal? CgstRate { get; set; }
    [Display(Name = "Sgst Rate")] public decimal? SgstRate { get; set; }
    [Display(Name = "Igst Rate")] public decimal? IgstRate { get; set; }
    [Display(Name = "Cess Rate")] public decimal? CessRate { get; set; }
    [Display(Name = "Price Threshold From")] public decimal? PriceThresholdFrom { get; set; }
    [Display(Name = "Price Threshold To")] public decimal? PriceThresholdTo { get; set; }
    [Display(Name = "Threshold Basis")] public string? ThresholdBasis { get; set; }
    [Display(Name = "Intra State Formula")] public string? IntraStateFormula { get; set; }
    [Display(Name = "Inter State Formula")] public string? InterStateFormula { get; set; }
    [Display(Name = "Effective From")] public DateTime EffectiveFrom { get; set; }
    [Display(Name = "Effective To")] public DateTime? EffectiveTo { get; set; }
    [Display(Name = "Priority")] public int Priority { get; set; } = 100;
    [Display(Name = "Is Active")] public bool IsActive { get; set; } = true;
    [Display(Name = "Notes")] public string? Notes { get; set; }
}

/// <summary>Indian GST state code master (global reference data, seeded once).</summary>
public class GstStateCode : BaseEntity
{
    public GstStateCode()
    {
        StateCode = string.Empty;
        StateName = string.Empty;
    }

    [Display(Name = "State Code")] public string StateCode { get; set; }
    [Display(Name = "State Name")] public string StateName { get; set; }
    [Display(Name = "Is Union Territory")] public bool IsUnionTerritory { get; set; }
    [Display(Name = "Is Other Territory")] public bool IsOtherTerritory { get; set; }
}

/// <summary>Unit Quantity Code master (global reference data, seeded once).</summary>
public class GstUqcCode : BaseEntity
{
    public GstUqcCode()
    {
        UqcCode = string.Empty;
        Description = string.Empty;
    }

    [Display(Name = "Uqc Code")] public string UqcCode { get; set; }
    [Display(Name = "Description")] public string Description { get; set; }
    [Display(Name = "Is Active")] public bool IsActive { get; set; } = true;
}

/// <summary>Configurable GST validation rule (severity/strict-mode per rule) driving the audit engine.</summary>
public class GstAuditRule : BaseEntity
{
    public GstAuditRule()
    {
        RuleCode = string.Empty;
        RuleName = string.Empty;
        ModuleArea = string.Empty;
        Severity = "Warning";
    }

    [Display(Name = "Rule Code")] public string RuleCode { get; set; }
    [Display(Name = "Rule Name")] public string RuleName { get; set; }
    [Display(Name = "Module Area")] public string ModuleArea { get; set; }
    [Display(Name = "Severity")] public string Severity { get; set; }
    [Display(Name = "Is Enabled")] public bool IsEnabled { get; set; } = true;
    [Display(Name = "Strict Mode")] public bool StrictMode { get; set; }
    [Display(Name = "Config JSON")] public string ConfigJson { get; set; } = "{}";
    [Display(Name = "Message Template")] public string? MessageTemplate { get; set; }
}

/// <summary>A single audit-engine finding against a sale/purchase/product/party record.</summary>
public class GstAuditFinding : CompanyBase
{
    public GstAuditFinding()
    {
        RuleCode = string.Empty;
        Severity = "Warning";
        ModuleArea = string.Empty;
        EntityType = string.Empty;
        Message = string.Empty;
        Status = "Open";
    }

    [Display(Name = "Rule Code")] public string RuleCode { get; set; }
    [Display(Name = "Severity")] public string Severity { get; set; }
    [Display(Name = "Module Area")] public string ModuleArea { get; set; }
    [Display(Name = "Entity Type")] public string EntityType { get; set; }
    [Display(Name = "Entity Id")] public Guid? EntityId { get; set; }
    [Display(Name = "Invoice Id")] public Guid? InvoiceId { get; set; }
    [Display(Name = "Purchase Invoice Id")] public Guid? PurchaseInvoiceId { get; set; }
    [Display(Name = "Product Id")] public Guid? ProductId { get; set; }
    [Display(Name = "Customer Id")] public Guid? CustomerId { get; set; }
    [Display(Name = "Vendor Id")] public Guid? VendorId { get; set; }
    [Display(Name = "Line Id")] public Guid? LineId { get; set; }
    [Display(Name = "Gstin")] public string? Gstin { get; set; }
    [Display(Name = "Hsn Code")] public string? HsnCode { get; set; }
    [Display(Name = "Message")] public string Message { get; set; }
    [Display(Name = "Expected Value")] public string? ExpectedValue { get; set; }
    [Display(Name = "Actual Value")] public string? ActualValue { get; set; }
    [Display(Name = "Suggested Fix JSON")] public string? SuggestedFixJson { get; set; }
    [Display(Name = "Status")] public string Status { get; set; }
    [Display(Name = "Reviewed By")] public string? ReviewedBy { get; set; }
    [Display(Name = "Reviewed At")] public DateTime? ReviewedAt { get; set; }
}

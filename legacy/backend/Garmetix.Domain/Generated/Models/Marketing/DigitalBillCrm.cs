using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.Marketing;

public class DigitalInvoice : StoreBase
{
    [Required] public Guid InvoiceId { get; set; }
    [MaxLength(40)] public string InvoiceType { get; set; } = "Sale";
    [Required, MaxLength(80)] public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    [MaxLength(160)] public string CustomerName { get; set; } = string.Empty;
    [MaxLength(30)] public string CustomerMobile { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    [Required, MaxLength(80)] public string PublicToken { get; set; } = string.Empty;
    [MaxLength(300)] public string? PublicUrl { get; set; }
    [MaxLength(300)] public string? PdfPath { get; set; }
    public string? HtmlSnapshotJson { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? DisabledAt { get; set; }
    [MaxLength(120)] public string? DisabledBy { get; set; }
    [MaxLength(300)] public string? DisableReason { get; set; }
    public DateTime? LastOpenedAt { get; set; }
    public int OpenCount { get; set; }
    public int PdfDownloadCount { get; set; }
    public int ReviewClickCount { get; set; }
    public int FeedbackCount { get; set; }
    [MaxLength(40)] public string WhatsAppStatus { get; set; } = "NotConfigured";
    public DateTime? LastWhatsAppSentAt { get; set; }
}

public class DigitalInvoiceEvent : StoreBase
{
    public Guid DigitalInvoiceId { get; set; }
    [Required, MaxLength(60)] public string EventType { get; set; } = string.Empty;
    [MaxLength(80)] public string? Source { get; set; }
    [MaxLength(120)] public string? IpAddress { get; set; }
    [MaxLength(500)] public string? UserAgent { get; set; }
    [MaxLength(500)] public string? TargetUrl { get; set; }
    public string? DetailsJson { get; set; }
    public DateTime EventAt { get; set; } = DateTime.UtcNow;
}

public class StoreReviewSetting : StoreBase
{
    [MaxLength(500)] public string? GoogleReviewUrl { get; set; }
    [MaxLength(500)] public string? InstagramUrl { get; set; }
    [MaxLength(500)] public string? FacebookUrl { get; set; }
    [MaxLength(30)] public string? WhatsAppSupportNumber { get; set; }
    public bool EnableGoogleReview { get; set; } = true;
    public bool EnableInstagram { get; set; } = true;
    public bool EnableFacebook { get; set; } = false;
    public bool EnableWhatsappSupport { get; set; } = true;
    public bool EnablePrivateFeedback { get; set; } = true;
    [MaxLength(160)] public string? ReviewButtonText { get; set; } = "Share your honest Google review";
    [MaxLength(160)] public string? FeedbackButtonText { get; set; } = "Share private feedback";
}

public class CustomerFeedback : StoreBase
{
    public Guid DigitalInvoiceId { get; set; }
    public Guid? CustomerId { get; set; }
    [MaxLength(160)] public string CustomerName { get; set; } = string.Empty;
    [MaxLength(30)] public string CustomerMobile { get; set; } = string.Empty;
    public int Rating { get; set; }
    [MaxLength(1000)] public string? Message { get; set; }
    [MaxLength(60)] public string Source { get; set; } = "DigitalInvoice";
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}


public class WhatsAppProviderSetting : StoreBase
{
    public bool IsEnabled { get; set; } = false;
    public bool AutoSendDigitalBills { get; set; } = false;
    [MaxLength(80)] public string Provider { get; set; } = "ManualOnly";
    [MaxLength(500)] public string? ApiBaseUrl { get; set; }
    [MaxLength(1000)] public string? ApiToken { get; set; }
    [MaxLength(120)] public string? PhoneNumberId { get; set; }
    [MaxLength(120)] public string? SenderId { get; set; }
    [MaxLength(120)] public string? TemplateName { get; set; }
    [MaxLength(20)] public string LanguageCode { get; set; } = "en";
    [MaxLength(2000)] public string MessageTemplateText { get; set; } = "Hello {{customerName}}, thank you for shopping at {{storeName}}. Your invoice {{invoiceNumber}} of ₹{{amount}} is ready. View bill: {{publicUrl}}";
    public bool SendPdfLink { get; set; } = true;
    public bool FallbackToManualLog { get; set; } = true;
    public int RetryLimit { get; set; } = 3;
    public DateTime? LastTestAt { get; set; }
    [MaxLength(1000)] public string? LastError { get; set; }
}

public class WhatsAppMessageLog : StoreBase
{
    public Guid? DigitalInvoiceId { get; set; }
    [MaxLength(30)] public string CustomerMobile { get; set; } = string.Empty;
    [MaxLength(80)] public string Provider { get; set; } = "ManualOnly";
    [MaxLength(120)] public string? TemplateName { get; set; }
    [MaxLength(2000)] public string MessageBody { get; set; } = string.Empty;
    [MaxLength(60)] public string Status { get; set; } = "Queued";
    [MaxLength(160)] public string? ProviderMessageId { get; set; }
    [MaxLength(1000)] public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }
}

public class InvoiceAdBanner : CompanyBase
{
    public Guid? StoreGroupId { get; set; }
    public Guid? StoreId { get; set; }
    [Required, MaxLength(160)] public string Title { get; set; } = string.Empty;
    [Required, MaxLength(500)] public string ImageUrl { get; set; } = string.Empty;
    [MaxLength(500)] public string? TargetUrl { get; set; }
    [MaxLength(40)] public string Position { get; set; } = "Footer";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; }
    public int ClickCount { get; set; }
}
public class DigitalBillCampaign : CompanyBase
{
    public Guid? StoreGroupId { get; set; }
    public Guid? StoreId { get; set; }
    [Required, MaxLength(160)] public string Name { get; set; } = string.Empty;
    [MaxLength(60)] public string Segment { get; set; } = "all";
    public DateTime FromDate { get; set; } = DateTime.UtcNow.Date.AddDays(-29);
    public DateTime ToDate { get; set; } = DateTime.UtcNow.Date;
    [MaxLength(160)] public string? SearchText { get; set; }
    [MaxLength(60)] public string Channel { get; set; } = "WhatsAppManual";
    [MaxLength(60)] public string Status { get; set; } = "Draft";
    [MaxLength(160)] public string? TemplateName { get; set; }
    [MaxLength(200)] public string MessageTitle { get; set; } = "Digital bill campaign";
    [MaxLength(2000)] public string MessageBody { get; set; } = string.Empty;
    [MaxLength(500)] public string? OfferUrl { get; set; }
    [MaxLength(2000)] public string? Notes { get; set; }
    public int RecipientCount { get; set; }
    public int PreparedCount { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public int OpenCountAtCreate { get; set; }
    public int ReviewClickCountAtCreate { get; set; }
    public int FeedbackCountAtCreate { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime? QueuedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}

public class DigitalBillCampaignRecipient : StoreBase
{
    public Guid CampaignId { get; set; }
    public Guid DigitalInvoiceId { get; set; }
    [MaxLength(120)] public string AudienceKey { get; set; } = string.Empty;
    [MaxLength(160)] public string CustomerName { get; set; } = string.Empty;
    [MaxLength(30)] public string CustomerMobile { get; set; } = string.Empty;
    [MaxLength(80)] public string InvoiceNumber { get; set; } = string.Empty;
    [MaxLength(300)] public string PublicPath { get; set; } = string.Empty;
    public decimal LastInvoiceAmount { get; set; }
    [MaxLength(60)] public string Status { get; set; } = "Prepared";
    [MaxLength(2000)] public string MessageBody { get; set; } = string.Empty;
    [MaxLength(1000)] public string? ErrorMessage { get; set; }
    public DateTime? QueuedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? FailedAt { get; set; }
}


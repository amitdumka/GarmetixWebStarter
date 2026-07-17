using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.Communication;

/// <summary>
/// A static catalog of allowed string values for the Email queue/provider/template
/// pipeline, mirroring GstTaxCatalog - one source of truth for backend validation and
/// frontend dropdowns instead of hardcoding these lists in both places.
/// </summary>
public static class EmailCatalog
{
    public static class ProviderTypes
    {
        public const string BrevoApi = "BrevoApi";
        public const string Smtp = "Smtp";
        public const string LocalMasterOnly = "LocalMasterOnly";
    }

    /// <summary>SMTP presets supply default host/port/TLS values only - one SmtpEmailProviderClient implementation serves all of them.</summary>
    public static class SmtpPresets
    {
        public const string Brevo = "Brevo";
        public const string GoDaddyProfessionalEmail = "GoDaddyProfessionalEmail";
        public const string Microsoft365 = "Microsoft365";
        public const string Gmail = "Gmail";
        public const string Custom = "Custom";
        public const string LocalPostfixRelay = "LocalPostfixRelay";
    }

    public static class QueueStatuses
    {
        public const string Draft = "Draft";
        public const string Pending = "Pending";
        public const string Scheduled = "Scheduled";
        public const string Processing = "Processing";
        public const string Sent = "Sent";
        public const string Delivered = "Delivered";
        public const string Deferred = "Deferred";
        public const string Bounced = "Bounced";
        public const string Complained = "Complained";
        public const string Rejected = "Rejected";
        public const string Cancelled = "Cancelled";
        public const string Failed = "Failed";
        public const string DeadLetter = "DeadLetter";
    }

    public static class RecipientKinds
    {
        public const string To = "To";
        public const string Cc = "Cc";
        public const string Bcc = "Bcc";
    }

    public static class TemplateVersionStatuses
    {
        public const string Draft = "Draft";
        public const string PendingApproval = "PendingApproval";
        public const string Approved = "Approved";
        public const string Archived = "Archived";
    }

    public static class DeliveryEventTypes
    {
        public const string Sent = "Sent";
        public const string Delivered = "Delivered";
        public const string Deferred = "Deferred";
        public const string Bounced = "Bounced";
        public const string Blocked = "Blocked";
        public const string Invalid = "Invalid";
        public const string Complaint = "Complaint";
        public const string Unsubscribe = "Unsubscribe";
        public const string Opened = "Opened";
        public const string Clicked = "Clicked";
    }

    public static class SuppressionReasons
    {
        public const string HardBounce = "HardBounce";
        public const string Complaint = "Complaint";
        public const string Invalid = "Invalid";
        public const string Manual = "Manual";
    }
}

/// <summary>
/// A configured outbound email provider (Brevo API or SMTP). Scoping is nullable by
/// design, same as GstApiProvider: null Store/StoreGroup/Company means the provider
/// applies globally, letting resolution fall back Store -> Company -> Tenant default -> LocalMasterOnly.
/// </summary>
public class EmailProviderConfiguration : BaseEntity
{
    public EmailProviderConfiguration()
    {
        ProviderName = string.Empty;
        ProviderType = EmailCatalog.ProviderTypes.LocalMasterOnly;
        FromEmail = string.Empty;
        FromName = "Garmetix";
    }

    [Display(Name = "Company Id")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group Id")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store Id")] public Guid? StoreId { get; set; }
    [Display(Name = "Provider Name")] public string ProviderName { get; set; }
    [Display(Name = "Provider Type")] public string ProviderType { get; set; }
    [Display(Name = "Smtp Preset Key")] public string? SmtpPresetKey { get; set; }
    [Display(Name = "Host")] public string? Host { get; set; }
    [Display(Name = "Port")] public int? Port { get; set; }
    [Display(Name = "Enable Ssl")] public bool EnableSsl { get; set; } = true;
    [Display(Name = "Use Start Tls")] public bool UseStartTls { get; set; } = true;
    [Display(Name = "From Email")] public string FromEmail { get; set; }
    [Display(Name = "From Name")] public string FromName { get; set; }
    [Display(Name = "Reply To Email")] public string? ReplyToEmail { get; set; }
    [Display(Name = "Is Enabled")] public bool IsEnabled { get; set; } = true;
    [Display(Name = "Is Default")] public bool IsDefault { get; set; }
    [Display(Name = "Priority")] public int Priority { get; set; } = 100;
    [Display(Name = "Timeout Seconds")] public int TimeoutSeconds { get; set; } = 30;
    [Display(Name = "Max Retries")] public int MaxRetries { get; set; } = 3;
    [Display(Name = "Daily Rate Limit")] public int? DailyRateLimit { get; set; }
    [Display(Name = "Per Minute Rate Limit")] public int? PerMinuteRateLimit { get; set; }
    [Display(Name = "Notes")] public string? Notes { get; set; }
    [Display(Name = "Created By")] public string? CreatedBy { get; set; }
    [Display(Name = "Updated By")] public string? UpdatedBy { get; set; }
    [Display(Name = "Revision")] public int Revision { get; set; }
}

/// <summary>
/// Encrypted provider secrets (SMTP password, Brevo API key, etc). EncryptedValue always
/// holds ciphertext via EmailCredentialProtector - decrypted values never reach the frontend.
/// </summary>
public class EmailProviderCredential : BaseEntity
{
    public EmailProviderCredential()
    {
        CredentialKey = string.Empty;
        EncryptedValue = string.Empty;
    }

    [Display(Name = "Provider Id")] public Guid ProviderId { get; set; }
    [Display(Name = "Credential Key")] public string CredentialKey { get; set; }
    [Display(Name = "Encrypted Value")] public string EncryptedValue { get; set; }
    [Display(Name = "Masked Display Value")] public string? MaskedDisplayValue { get; set; }
}

/// <summary>A named, versioned email template (e.g. "sale-invoice", "payslip"). CurrentVersionId points at the live/published version.</summary>
public class EmailTemplate : BaseEntity
{
    public EmailTemplate()
    {
        TemplateKey = string.Empty;
        DisplayName = string.Empty;
    }

    [Display(Name = "Company Id")] public Guid? CompanyId { get; set; }
    [Display(Name = "Template Key")] public string TemplateKey { get; set; }
    [Display(Name = "Display Name")] public string DisplayName { get; set; }
    [Display(Name = "Category")] public string? Category { get; set; }
    [Display(Name = "Is System Template")] public bool IsSystemTemplate { get; set; }
    [Display(Name = "Is Active")] public bool IsActive { get; set; } = true;
    [Display(Name = "Current Version Id")] public Guid? CurrentVersionId { get; set; }
    [Display(Name = "Revision")] public int Revision { get; set; }
}

/// <summary>One version of an EmailTemplate's content. Subject/HtmlBody/TextBody hold Handlebars-style {{tokens}}, escaped/sanitized at render time.</summary>
public class EmailTemplateVersion : BaseEntity
{
    public EmailTemplateVersion()
    {
        Subject = string.Empty;
        HtmlBody = string.Empty;
        Status = EmailCatalog.TemplateVersionStatuses.Draft;
    }

    [Display(Name = "Template Id")] public Guid TemplateId { get; set; }
    [Display(Name = "Version Number")] public int VersionNumber { get; set; }
    [Display(Name = "Subject")] public string Subject { get; set; }
    [Display(Name = "Html Body")] public string HtmlBody { get; set; }
    [Display(Name = "Text Body")] public string? TextBody { get; set; }
    [Display(Name = "Sample Data Json")] public string? SampleDataJson { get; set; }
    [Display(Name = "Status")] public string Status { get; set; }
    [Display(Name = "Approved By User Id")] public Guid? ApprovedByUserId { get; set; }
    [Display(Name = "Approved At")] public DateTime? ApprovedAtUtc { get; set; }
    [Display(Name = "Created By User Id")] public Guid? CreatedByUserId { get; set; }
}

/// <summary>
/// The durable outbox/queue row for one outbound email. Idempotency key is
/// tenant+event+entity+template+recipient+revision (deterministic) so a duplicate
/// enqueue attempt (e.g. a retried business-event publish) never creates a second send;
/// a genuine resend instead creates a brand new row linked via ResentFromQueueItemId.
/// </summary>
public class EmailQueueItem : BaseEntity
{
    public EmailQueueItem()
    {
        Status = EmailCatalog.QueueStatuses.Draft;
        Subject = string.Empty;
        HtmlBody = string.Empty;
        IdempotencyKey = string.Empty;
        CorrelationId = string.Empty;
    }

    [Display(Name = "Company Id")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group Id")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store Id")] public Guid? StoreId { get; set; }
    [Display(Name = "Source Module")] public string? SourceModule { get; set; }
    [Display(Name = "Source Type")] public string? SourceType { get; set; }
    [Display(Name = "Source Id")] public Guid? SourceId { get; set; }
    [Display(Name = "Template Id")] public Guid? TemplateId { get; set; }
    [Display(Name = "Template Version Id")] public Guid? TemplateVersionId { get; set; }
    [Display(Name = "Correlation Id")] public string CorrelationId { get; set; }
    [Display(Name = "Idempotency Key")] public string IdempotencyKey { get; set; }
    [Display(Name = "Provider Id Used")] public Guid? ProviderIdUsed { get; set; }
    [Display(Name = "Status")] public string Status { get; set; }
    [Display(Name = "Subject")] public string Subject { get; set; }
    [Display(Name = "Html Body")] public string HtmlBody { get; set; }
    [Display(Name = "Text Body")] public string? TextBody { get; set; }
    [Display(Name = "Scheduled For")] public DateTime? ScheduledForUtc { get; set; }
    [Display(Name = "Processing Lease Until")] public DateTime? ProcessingLeaseUntilUtc { get; set; }
    [Display(Name = "Processing Lease Owner")] public string? ProcessingLeaseOwner { get; set; }
    [Display(Name = "Attempt Count")] public int AttemptCount { get; set; }
    [Display(Name = "Max Attempts")] public int MaxAttempts { get; set; } = 8;
    [Display(Name = "Next Attempt At")] public DateTime? NextAttemptAtUtc { get; set; }
    [Display(Name = "Last Error Code")] public string? LastErrorCode { get; set; }
    [Display(Name = "Last Error Message")] public string? LastErrorMessage { get; set; }
    [Display(Name = "Provider Message Id")] public string? ProviderMessageId { get; set; }
    [Display(Name = "Resent From Queue Item Id")] public Guid? ResentFromQueueItemId { get; set; }
    [Display(Name = "Created By User Id")] public Guid? CreatedByUserId { get; set; }
    [Display(Name = "Revision")] public int Revision { get; set; }
}

/// <summary>One resolved recipient (To/Cc/Bcc) of an EmailQueueItem.</summary>
public class EmailRecipient : BaseEntity
{
    public EmailRecipient()
    {
        Kind = EmailCatalog.RecipientKinds.To;
        EmailAddress = string.Empty;
    }

    [Display(Name = "Queue Item Id")] public Guid QueueItemId { get; set; }
    [Display(Name = "Kind")] public string Kind { get; set; }
    [Display(Name = "Email Address")] public string EmailAddress { get; set; }
    [Display(Name = "Display Name")] public string? DisplayName { get; set; }
}

/// <summary>An attachment on an outbound EmailQueueItem. Same storage pattern as CommunicationAttachment.</summary>
public class EmailAttachment : BaseEntity
{
    public EmailAttachment()
    {
        OriginalFileName = string.Empty;
        StoredFileName = string.Empty;
        StoredRelativePath = string.Empty;
        ContentType = "application/octet-stream";
        Sha256Checksum = string.Empty;
    }

    [Display(Name = "Queue Item Id")] public Guid QueueItemId { get; set; }
    [Display(Name = "Original File Name")] public string OriginalFileName { get; set; }
    [Display(Name = "Stored File Name")] public string StoredFileName { get; set; }
    [Display(Name = "Stored Relative Path")] public string StoredRelativePath { get; set; }
    [Display(Name = "Content Type")] public string ContentType { get; set; }
    [Display(Name = "Size Bytes")] public long SizeBytes { get; set; }
    [Display(Name = "Sha256 Checksum")] public string Sha256Checksum { get; set; }
}

/// <summary>One provider-send attempt for a queue item (success or failure). Errors are sanitized - never contains secrets/auth headers.</summary>
public class EmailDeliveryAttempt : BaseEntity
{
    [Display(Name = "Queue Item Id")] public Guid QueueItemId { get; set; }
    [Display(Name = "Attempt Number")] public int AttemptNumber { get; set; }
    [Display(Name = "Provider Id")] public Guid? ProviderId { get; set; }
    [Display(Name = "Started At")] public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    [Display(Name = "Completed At")] public DateTime? CompletedAtUtc { get; set; }
    [Display(Name = "Was Success")] public bool WasSuccess { get; set; }
    [Display(Name = "Response Status Code")] public int? ResponseStatusCode { get; set; }
    [Display(Name = "Error Code")] public string? ErrorCode { get; set; }
    [Display(Name = "Error Message")] public string? ErrorMessage { get; set; }
    [Display(Name = "Duration Ms")] public int? DurationMs { get; set; }
}

/// <summary>
/// An append-only, deduplicated delivery event observed from a provider webhook
/// (or synthesized locally for Sent). ProviderEventId is the dedup key; OccurredAtUtc
/// preserves the provider's own event time, not our ingestion time.
/// </summary>
public class EmailDeliveryEvent : BaseEntity
{
    public EmailDeliveryEvent()
    {
        EventType = EmailCatalog.DeliveryEventTypes.Sent;
    }

    [Display(Name = "Queue Item Id")] public Guid? QueueItemId { get; set; }
    [Display(Name = "Provider Message Id")] public string? ProviderMessageId { get; set; }
    [Display(Name = "Event Type")] public string EventType { get; set; }
    [Display(Name = "Provider Event Id")] public string? ProviderEventId { get; set; }
    [Display(Name = "Occurred At")] public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    [Display(Name = "Raw Payload Sanitized Json")] public string? RawPayloadSanitizedJson { get; set; }
}

/// <summary>A scoped suppressed email address (hard bounce, complaint, invalid, or manual). Never leaks across unrelated tenants - CompanyId scopes it.</summary>
public class EmailSuppressionEntry : BaseEntity
{
    public EmailSuppressionEntry()
    {
        EmailAddress = string.Empty;
        Reason = EmailCatalog.SuppressionReasons.Manual;
    }

    [Display(Name = "Company Id")] public Guid? CompanyId { get; set; }
    [Display(Name = "Email Address")] public string EmailAddress { get; set; }
    [Display(Name = "Reason")] public string Reason { get; set; }
    [Display(Name = "Source Event Id")] public Guid? SourceEventId { get; set; }
    [Display(Name = "Is Active")] public bool IsActive { get; set; } = true;
    [Display(Name = "Removed By User Id")] public Guid? RemovedByUserId { get; set; }
    [Display(Name = "Removed At")] public DateTime? RemovedAtUtc { get; set; }
    [Display(Name = "Removal Reason")] public string? RemovalReason { get; set; }
}

/// <summary>A per-day usage bucket used for provider rate limiting.</summary>
public class EmailUsageCounter : BaseEntity
{
    public EmailUsageCounter()
    {
        PeriodKey = string.Empty;
    }

    [Display(Name = "Provider Id")] public Guid? ProviderId { get; set; }
    [Display(Name = "Company Id")] public Guid? CompanyId { get; set; }
    [Display(Name = "Period Key")] public string PeriodKey { get; set; }
    [Display(Name = "Sent Count")] public int SentCount { get; set; }
    [Display(Name = "Failed Count")] public int FailedCount { get; set; }
}

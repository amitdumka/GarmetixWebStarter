using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.Communication;

/// <summary>
/// A static catalog of allowed string values for Communication & Mail status/kind fields,
/// mirroring the GstTaxCatalog pattern (one source of truth instead of hardcoding these
/// lists in both backend validation and frontend dropdowns).
/// </summary>
public static class CommunicationCatalog
{
    public static class ConversationTypes
    {
        public const string Direct = "Direct";
        public const string Group = "Group";
        public const string Broadcast = "Broadcast";
    }

    public static class MessagePriorities
    {
        public const string Normal = "Normal";
        public const string High = "High";
        public const string Urgent = "Urgent";
    }

    public static class FolderStates
    {
        public const string Inbox = "Inbox";
        public const string Archived = "Archived";
        public const string Trashed = "Trashed";
    }

    public static class DigestFrequencies
    {
        public const string Immediate = "Immediate";
        public const string Daily = "Daily";
        public const string None = "None";
    }
}

/// <summary>
/// An internal conversation thread. Scoping is nullable-by-design like every other
/// module here (see WorkspaceScope) - a pure direct message between two users still
/// carries the sender's CompanyId/StoreGroupId/StoreId snapshot so tenant filtering works
/// without joining through messages.
/// </summary>
public class CommunicationConversation : BaseEntity
{
    public CommunicationConversation()
    {
        Subject = string.Empty;
        ConversationType = CommunicationCatalog.ConversationTypes.Direct;
    }

    [Display(Name = "Company Id")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group Id")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store Id")] public Guid? StoreId { get; set; }
    [Display(Name = "Subject")] public string Subject { get; set; }
    [Display(Name = "Conversation Type")] public string ConversationType { get; set; }
    [Display(Name = "Source Module")] public string? SourceModule { get; set; }
    [Display(Name = "Source Type")] public string? SourceType { get; set; }
    [Display(Name = "Source Id")] public Guid? SourceId { get; set; }
    [Display(Name = "Created By User Id")] public Guid CreatedByUserId { get; set; }
    [Display(Name = "Last Message At")] public DateTime LastMessageAtUtc { get; set; } = DateTime.UtcNow;
    [Display(Name = "Revision")] public int Revision { get; set; }
}

/// <summary>
/// One message inside a conversation. Internal messages are deliberately plain-text-only
/// (BodyFormat = PlainText) to keep the internal-mail XSS surface minimal - rich HTML is
/// reserved for outbound EmailTemplate rendering, which has its own sanitization pipeline.
/// </summary>
public class CommunicationMessage : BaseEntity
{
    public CommunicationMessage()
    {
        Body = string.Empty;
        BodyFormat = "PlainText";
        Priority = CommunicationCatalog.MessagePriorities.Normal;
    }

    [Display(Name = "Conversation Id")] public Guid ConversationId { get; set; }
    [Display(Name = "Company Id")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group Id")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store Id")] public Guid? StoreId { get; set; }
    [Display(Name = "Sender User Id")] public Guid SenderUserId { get; set; }
    [Display(Name = "Sender Name Snapshot")] public string? SenderNameSnapshot { get; set; }
    [Display(Name = "Body")] public string Body { get; set; }
    [Display(Name = "Body Format")] public string BodyFormat { get; set; }
    [Display(Name = "Priority")] public string Priority { get; set; }
    [Display(Name = "Is Draft")] public bool IsDraft { get; set; }
    [Display(Name = "Reply To Message Id")] public Guid? ReplyToMessageId { get; set; }
    [Display(Name = "Sent At")] public DateTime? SentAtUtc { get; set; }
    [Display(Name = "Revision")] public int Revision { get; set; }
}

/// <summary>
/// A resolved, snapshotted recipient of one message (one row per message per recipient,
/// unique on MessageId+RecipientUserId). Role/department/store audiences are expanded
/// into individual rows at send time (per architecture doc section "Recipient
/// resolution") so a later role change never rewrites history. Archive/Trash/read-state
/// live here (per-recipient-per-message), not on the message or conversation; "archiving
/// a conversation" is a bulk update of every CommunicationRecipient row that user holds
/// across that conversation, done by the application service, not a separate membership row.
/// </summary>
public class CommunicationRecipient : BaseEntity
{
    public CommunicationRecipient()
    {
        FolderState = CommunicationCatalog.FolderStates.Inbox;
    }

    [Display(Name = "Message Id")] public Guid MessageId { get; set; }
    [Display(Name = "Conversation Id")] public Guid ConversationId { get; set; }
    [Display(Name = "Recipient User Id")] public Guid RecipientUserId { get; set; }
    [Display(Name = "Is Read")] public bool IsRead { get; set; }
    [Display(Name = "Read At")] public DateTime? ReadAtUtc { get; set; }
    [Display(Name = "Folder State")] public string FolderState { get; set; }
    [Display(Name = "Trashed At")] public DateTime? TrashedAtUtc { get; set; }
}

/// <summary>
/// An attachment on an internal message. Storage follows the PurchaseInvoiceImportService/
/// SwalekhaDocumentEndpoints pattern: configurable storage root, per-company/date subfolders,
/// random non-guessable stored file name, checksum, allow-listed extension.
/// </summary>
public class CommunicationAttachment : BaseEntity
{
    public CommunicationAttachment()
    {
        OriginalFileName = string.Empty;
        StoredFileName = string.Empty;
        StoredRelativePath = string.Empty;
        ContentType = "application/octet-stream";
        Sha256Checksum = string.Empty;
    }

    [Display(Name = "Message Id")] public Guid MessageId { get; set; }
    [Display(Name = "Company Id")] public Guid? CompanyId { get; set; }
    [Display(Name = "Original File Name")] public string OriginalFileName { get; set; }
    [Display(Name = "Stored File Name")] public string StoredFileName { get; set; }
    [Display(Name = "Stored Relative Path")] public string StoredRelativePath { get; set; }
    [Display(Name = "Content Type")] public string ContentType { get; set; }
    [Display(Name = "Size Bytes")] public long SizeBytes { get; set; }
    [Display(Name = "Sha256 Checksum")] public string Sha256Checksum { get; set; }
    [Display(Name = "Uploaded By User Id")] public Guid UploadedByUserId { get; set; }
}

/// <summary>Per-user notification preferences for internal communication and outbound-email echoes.</summary>
public class CommunicationPreference : BaseEntity
{
    public CommunicationPreference()
    {
        DigestFrequency = CommunicationCatalog.DigestFrequencies.Immediate;
    }

    [Display(Name = "User Id")] public Guid UserId { get; set; }
    [Display(Name = "Company Id")] public Guid? CompanyId { get; set; }
    [Display(Name = "Email Notifications Enabled")] public bool EmailNotificationsEnabled { get; set; } = true;
    [Display(Name = "Notify On Direct Message")] public bool NotifyOnDirectMessage { get; set; } = true;
    [Display(Name = "Notify On Broadcast")] public bool NotifyOnBroadcast { get; set; } = true;
    [Display(Name = "Digest Frequency")] public string DigestFrequency { get; set; }
}

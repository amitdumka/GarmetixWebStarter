namespace Garmetix.Api.Communication;

public sealed record CommunicationComposeRequest(
    string Subject,
    string Body,
    string Priority,
    List<Guid> RecipientUserIds,
    List<string> RecipientRoles,
    bool SaveAsDraft);

public sealed record CommunicationDraftUpdateRequest(
    string Subject,
    string Body,
    string Priority,
    List<Guid> RecipientUserIds,
    List<string> RecipientRoles);

public sealed record CommunicationReplyRequest(string Body, bool ReplyAll);

public sealed record CommunicationRecipientDto(Guid RecipientUserId, string? RecipientName, bool IsRead, DateTime? ReadAtUtc, string FolderState);

public sealed record CommunicationAttachmentDto(Guid Id, string OriginalFileName, string ContentType, long SizeBytes);

public sealed record CommunicationMessageDto(
    Guid Id,
    Guid ConversationId,
    Guid SenderUserId,
    string? SenderNameSnapshot,
    string Body,
    string Priority,
    bool IsDraft,
    DateTime? SentAtUtc,
    DateTime CreatedAt,
    List<CommunicationRecipientDto> Recipients,
    List<CommunicationAttachmentDto> Attachments);

public sealed record CommunicationConversationSummaryDto(
    Guid ConversationId,
    string Subject,
    string ConversationType,
    DateTime LastMessageAtUtc,
    bool HasUnread,
    string LastMessagePreview,
    string? LastSenderName,
    string FolderState);

public sealed record CommunicationConversationDetailDto(Guid ConversationId, string Subject, string ConversationType, List<CommunicationMessageDto> Messages);

public sealed record CommunicationPreferenceDto(bool EmailNotificationsEnabled, bool NotifyOnDirectMessage, bool NotifyOnBroadcast, string DigestFrequency);

public sealed record CommunicationPreferenceUpdateRequest(bool EmailNotificationsEnabled, bool NotifyOnDirectMessage, bool NotifyOnBroadcast, string DigestFrequency);

namespace Garmetix.Api.Communication;

public sealed record EmailAddressValue(string Email, string? DisplayName);

public sealed record EmailAttachmentPayload(string FileName, string ContentType, byte[] Content);

/// <summary>
/// A provider-neutral outbound send request. Every ITransactionalEmailProviderClient
/// implementation must accept exactly this shape - callers never depend on a specific
/// provider's request format.
/// </summary>
public sealed record EmailSendRequest(
    EmailAddressValue From,
    IReadOnlyList<EmailAddressValue> To,
    string Subject,
    string HtmlBody,
    string? TextBody,
    string? ReplyToEmail = null,
    IReadOnlyList<EmailAddressValue>? Cc = null,
    IReadOnlyList<EmailAddressValue>? Bcc = null,
    IReadOnlyList<EmailAttachmentPayload>? Attachments = null);

/// <summary>
/// A provider-neutral send result. ErrorMessage/RawResponseForLogging must already be
/// sanitized by the client that produced them - never contains secrets, auth headers, or
/// full provider payloads with sensitive data (see docs/communication-mail-security.md).
/// </summary>
public sealed record EmailSendResult(
    bool IsSuccess,
    string? ProviderMessageId,
    string? ErrorCode,
    string? ErrorMessage,
    bool IsTransientFailure,
    string? RawResponseForLogging = null)
{
    public static EmailSendResult Success(string? providerMessageId, string? rawResponseForLogging = null) =>
        new(true, providerMessageId, null, null, false, rawResponseForLogging);

    public static EmailSendResult PermanentFailure(string errorCode, string errorMessage, string? rawResponseForLogging = null) =>
        new(false, null, errorCode, errorMessage, false, rawResponseForLogging);

    public static EmailSendResult TransientFailure(string errorCode, string errorMessage, string? rawResponseForLogging = null) =>
        new(false, null, errorCode, errorMessage, true, rawResponseForLogging);
}

public sealed record EmailConnectionTestResult(bool IsSuccess, string Message);

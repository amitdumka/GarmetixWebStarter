using System.Security.Cryptography;
using System.Text;

namespace Garmetix.Api.Communication;

/// <summary>
/// Builds the deterministic idempotency key for an EmailQueueItem: tenant + event + entity +
/// template + recipient + revision. Same inputs always produce the same key, so a duplicate
/// enqueue attempt (e.g. a retried business-event publish) is rejected by the unique index on
/// EmailQueueItem.IdempotencyKey rather than creating a second send. A genuine resend must use
/// a bumped revision (or ResentFromQueueItemId linkage) to get a fresh key on purpose.
/// </summary>
public static class EmailIdempotencyKeyBuilder
{
    public static string Build(
        Guid? companyId,
        string sourceModule,
        string sourceType,
        Guid sourceId,
        string templateKey,
        string recipientEmail,
        int revision)
    {
        var normalizedRecipient = recipientEmail.Trim().ToLowerInvariant();
        var raw = string.Join(
            '|',
            companyId?.ToString("N") ?? "global",
            sourceModule.Trim(),
            sourceType.Trim(),
            sourceId.ToString("N"),
            templateKey.Trim(),
            normalizedRecipient,
            revision.ToString());

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

using Garmetix.Core.Models.Communication;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Communication;

public sealed record EmailEnqueueRequest(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string SourceModule,
    string SourceType,
    Guid SourceId,
    string TemplateKey,
    Guid? TemplateId,
    Guid? TemplateVersionId,
    string Subject,
    string HtmlBody,
    string? TextBody,
    IReadOnlyList<EmailAddressValue> To,
    int Revision,
    Guid? CreatedByUserId,
    DateTime? ScheduledForUtc = null,
    IReadOnlyList<EmailAttachmentPayload>? Attachments = null);

/// <summary>
/// The single entry point business modules call to queue an outbound email (never a direct
/// provider call - see docs/communication-mail-architecture.md rule 2/3). Enqueue is
/// idempotent: if a row with the same deterministic IdempotencyKey already exists it is
/// returned as-is rather than creating a duplicate send, so a retried business-event publish
/// can never double-send. A genuine resend must go through ResendAsync, which creates a new
/// linked row with a bumped revision instead of mutating history.
/// </summary>
public sealed class EmailEnqueueService(GarmetixDbContext db, CommunicationAttachmentStorageService attachmentStorage)
{
    public async Task<EmailQueueItem> EnqueueAsync(EmailEnqueueRequest request, CancellationToken cancellationToken)
    {
        if (request.To.Count == 0)
        {
            throw new InvalidOperationException("At least one recipient is required to enqueue an email.");
        }

        // The idempotency key is derived from the first recipient - multi-recipient sends with
        // the same source/template/revision are intentionally one queue item with several
        // EmailRecipient rows, not one key per recipient.
        var idempotencyKey = EmailIdempotencyKeyBuilder.Build(
            request.CompanyId,
            request.SourceModule,
            request.SourceType,
            request.SourceId,
            request.TemplateKey,
            request.To[0].Email,
            request.Revision);

        var existing = await db.EmailQueueItems.FirstOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var queueItem = new EmailQueueItem
        {
            CompanyId = request.CompanyId,
            StoreGroupId = request.StoreGroupId,
            StoreId = request.StoreId,
            SourceModule = request.SourceModule,
            SourceType = request.SourceType,
            SourceId = request.SourceId,
            TemplateId = request.TemplateId,
            TemplateVersionId = request.TemplateVersionId,
            CorrelationId = Guid.NewGuid().ToString("N"),
            IdempotencyKey = idempotencyKey,
            Status = EmailCatalog.QueueStatuses.Pending,
            Subject = request.Subject,
            HtmlBody = request.HtmlBody,
            TextBody = request.TextBody,
            ScheduledForUtc = request.ScheduledForUtc,
            CreatedByUserId = request.CreatedByUserId,
        };

        db.EmailQueueItems.Add(queueItem);
        db.EmailRecipients.AddRange(request.To.Select(to => new EmailRecipient
        {
            QueueItemId = queueItem.Id,
            Kind = EmailCatalog.RecipientKinds.To,
            EmailAddress = to.Email,
            DisplayName = to.DisplayName,
        }));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost a race against a concurrent duplicate enqueue attempt for the same
            // idempotency key - the unique index rejected our insert. Return the row the
            // other caller just created instead of surfacing an error to a business flow
            // that must never block on email infrastructure.
            var winner = await db.EmailQueueItems.AsNoTracking().FirstOrDefaultAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);
            if (winner is not null)
            {
                return winner;
            }

            throw;
        }

        if (request.Attachments is { Count: > 0 })
        {
            foreach (var attachment in request.Attachments)
            {
                var (storedFileName, storedRelativePath, checksum) = await attachmentStorage.SaveEmailAttachmentAsync(
                    request.CompanyId, queueItem.Id, attachment.Content, attachment.FileName, cancellationToken);
                db.EmailAttachments.Add(new EmailAttachment
                {
                    QueueItemId = queueItem.Id,
                    OriginalFileName = attachment.FileName,
                    StoredFileName = storedFileName,
                    StoredRelativePath = storedRelativePath,
                    ContentType = attachment.ContentType,
                    SizeBytes = attachment.Content.LongLength,
                    Sha256Checksum = checksum,
                });
            }
            await db.SaveChangesAsync(cancellationToken);
        }

        return queueItem;
    }

    /// <summary>Creates a brand-new, linked queue item for a genuine resend. Never mutates the original row's history.</summary>
    public async Task<EmailQueueItem> ResendAsync(Guid originalQueueItemId, CancellationToken cancellationToken)
    {
        var original = await db.EmailQueueItems.AsNoTracking().FirstOrDefaultAsync(item => item.Id == originalQueueItemId, cancellationToken)
            ?? throw new InvalidOperationException($"Queue item {originalQueueItemId} not found.");
        var originalRecipients = await db.EmailRecipients.AsNoTracking().Where(r => r.QueueItemId == originalQueueItemId).ToListAsync(cancellationToken);

        var resend = new EmailQueueItem
        {
            CompanyId = original.CompanyId,
            StoreGroupId = original.StoreGroupId,
            StoreId = original.StoreId,
            SourceModule = original.SourceModule,
            SourceType = original.SourceType,
            SourceId = original.SourceId,
            TemplateId = original.TemplateId,
            TemplateVersionId = original.TemplateVersionId,
            CorrelationId = original.CorrelationId,
            IdempotencyKey = $"{original.IdempotencyKey}:resend:{Guid.NewGuid():N}",
            Status = EmailCatalog.QueueStatuses.Pending,
            Subject = original.Subject,
            HtmlBody = original.HtmlBody,
            TextBody = original.TextBody,
            ResentFromQueueItemId = original.Id,
        };

        db.EmailQueueItems.Add(resend);
        db.EmailRecipients.AddRange(originalRecipients.Select(r => new EmailRecipient
        {
            QueueItemId = resend.Id,
            Kind = r.Kind,
            EmailAddress = r.EmailAddress,
            DisplayName = r.DisplayName,
        }));

        await db.SaveChangesAsync(cancellationToken);
        return resend;
    }
}

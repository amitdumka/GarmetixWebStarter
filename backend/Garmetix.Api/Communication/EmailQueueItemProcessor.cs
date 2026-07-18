using Garmetix.Core.Models.Communication;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Communication;

/// <summary>
/// Processes one already-claimed (Status=Processing) EmailQueueItem end to end: resolve
/// provider, check suppression, check rate limit, send, record the attempt, and transition
/// status per EmailQueueStateMachine. Split out from EmailQueueWorker so the pipeline can be
/// invoked directly (e.g. by a future "process now" admin action) without a BackgroundService.
/// </summary>
public sealed class EmailQueueItemProcessor(
    GarmetixDbContext db,
    EmailProviderResolutionService providerResolution,
    IEmailProviderClientFactory clientFactory,
    EmailRateLimitService rateLimiter,
    CommunicationAttachmentStorageService attachmentStorage,
    EmailQueueOptions options)
{
    public async Task ProcessAsync(EmailQueueItem item, CancellationToken cancellationToken)
    {
        var recipients = await db.EmailRecipients.AsNoTracking().Where(r => r.QueueItemId == item.Id).ToListAsync(cancellationToken);
        var toRecipients = recipients.Where(r => r.Kind == EmailCatalog.RecipientKinds.To).ToList();

        var suppressed = await FindSuppressedRecipientAsync(item.CompanyId, toRecipients, cancellationToken);
        if (suppressed is not null)
        {
            await RecordAttemptAsync(item, providerId: null, EmailSendResult.PermanentFailure(
                "RecipientSuppressed", $"'{suppressed}' is on the suppression list for this scope."), cancellationToken);
            TransitionTo(item, EmailCatalog.QueueStatuses.Rejected, "RecipientSuppressed", $"'{suppressed}' is suppressed.");
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var resolved = await providerResolution.ResolveAsync(item.CompanyId, item.StoreGroupId, item.StoreId, cancellationToken);
        // LocalMasterOnly never represents a real external send (whether it's a persisted seed
        // row or the in-memory fallback ResolveAsync synthesizes when no row exists at all) -
        // ProviderIdUsed stays null in either case so delivery-attempt/usage-counter rows never
        // point at a provider id that doesn't correspond to an actual send attempt.
        item.ProviderIdUsed = resolved.Provider.ProviderType == EmailCatalog.ProviderTypes.LocalMasterOnly ? null : resolved.Provider.Id;

        if (await rateLimiter.IsDailyLimitExceededAsync(item.ProviderIdUsed, item.CompanyId, resolved.Provider.DailyRateLimit, cancellationToken))
        {
            DeferForRetry(item, "DailyRateLimitExceeded", "The provider's daily send limit has been reached for this scope; will retry after the limit window resets.");
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var attachments = await db.EmailAttachments.AsNoTracking().Where(a => a.QueueItemId == item.Id).ToListAsync(cancellationToken);
        var request = new EmailSendRequest(
            From: new EmailAddressValue(resolved.Provider.FromEmail, resolved.Provider.FromName),
            To: toRecipients.Select(r => new EmailAddressValue(r.EmailAddress, r.DisplayName)).ToList(),
            Subject: item.Subject,
            HtmlBody: item.HtmlBody,
            TextBody: item.TextBody,
            ReplyToEmail: resolved.Provider.ReplyToEmail,
            Cc: recipients.Where(r => r.Kind == EmailCatalog.RecipientKinds.Cc).Select(r => new EmailAddressValue(r.EmailAddress, r.DisplayName)).ToList(),
            Bcc: recipients.Where(r => r.Kind == EmailCatalog.RecipientKinds.Bcc).Select(r => new EmailAddressValue(r.EmailAddress, r.DisplayName)).ToList(),
            Attachments: attachments.Select(a =>
            {
                var absolutePath = attachmentStorage.ResolveAbsolutePath(a.StoredRelativePath);
                return new EmailAttachmentPayload(a.OriginalFileName, a.ContentType, File.Exists(absolutePath) ? File.ReadAllBytes(absolutePath) : []);
            }).ToList());

        var client = clientFactory.GetClient(resolved.Provider.ProviderType);
        var result = await client.SendAsync(resolved.Provider, resolved.Credentials, request, cancellationToken);

        await RecordAttemptAsync(item, item.ProviderIdUsed, result, cancellationToken);
        await rateLimiter.RecordAttemptAsync(item.ProviderIdUsed, item.CompanyId, result.IsSuccess, cancellationToken);

        if (result.IsSuccess)
        {
            item.ProviderMessageId = result.ProviderMessageId;
            TransitionTo(item, EmailCatalog.QueueStatuses.Sent, null, null);
            db.EmailDeliveryEvents.Add(new EmailDeliveryEvent
            {
                QueueItemId = item.Id,
                ProviderMessageId = result.ProviderMessageId,
                EventType = EmailCatalog.DeliveryEventTypes.Sent,
                OccurredAtUtc = DateTime.UtcNow,
            });
        }
        else if (result.IsTransientFailure)
        {
            if (item.AttemptCount >= item.MaxAttempts)
            {
                TransitionTo(item, EmailCatalog.QueueStatuses.DeadLetter, result.ErrorCode, result.ErrorMessage);
            }
            else
            {
                DeferForRetry(item, result.ErrorCode, result.ErrorMessage);
            }
        }
        else
        {
            TransitionTo(item, EmailCatalog.QueueStatuses.Rejected, result.ErrorCode, result.ErrorMessage);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private void DeferForRetry(EmailQueueItem item, string? errorCode, string? errorMessage)
    {
        TransitionTo(item, EmailCatalog.QueueStatuses.Deferred, errorCode, errorMessage);
        var backoff = EmailRetryBackoffCalculator.ComputeWithRandomJitter(
            item.AttemptCount,
            TimeSpan.FromSeconds(options.BaseBackoffSeconds),
            TimeSpan.FromSeconds(options.MaxBackoffSeconds),
            options.JitterRatio);
        item.NextAttemptAtUtc = DateTime.UtcNow.Add(backoff);
    }

    private static void TransitionTo(EmailQueueItem item, string newStatus, string? errorCode, string? errorMessage)
    {
        if (!EmailQueueStateMachine.CanTransition(item.Status, newStatus))
        {
            throw new InvalidOperationException($"Illegal EmailQueueItem transition {item.Status} -> {newStatus} for item {item.Id}.");
        }

        item.Status = newStatus;
        item.LastErrorCode = errorCode;
        item.LastErrorMessage = errorMessage;
        item.ProcessingLeaseOwner = null;
        item.ProcessingLeaseUntilUtc = null;
    }

    private async Task RecordAttemptAsync(EmailQueueItem item, Guid? providerId, EmailSendResult result, CancellationToken cancellationToken)
    {
        db.EmailDeliveryAttempts.Add(new EmailDeliveryAttempt
        {
            QueueItemId = item.Id,
            AttemptNumber = item.AttemptCount,
            ProviderId = providerId,
            StartedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = DateTime.UtcNow,
            WasSuccess = result.IsSuccess,
            ErrorCode = result.ErrorCode,
            ErrorMessage = result.ErrorMessage,
        });
        await Task.CompletedTask;
    }

    private async Task<string?> FindSuppressedRecipientAsync(Guid? companyId, IReadOnlyList<EmailRecipient> toRecipients, CancellationToken cancellationToken)
    {
        var addresses = toRecipients.Select(r => r.EmailAddress.Trim().ToLowerInvariant()).ToArray();
        if (addresses.Length == 0)
        {
            return null;
        }

        var suppressed = await db.EmailSuppressionEntries.AsNoTracking()
            .Where(s => s.IsActive && (s.CompanyId == null || s.CompanyId == companyId) && addresses.Contains(s.EmailAddress))
            .Select(s => s.EmailAddress)
            .FirstOrDefaultAsync(cancellationToken);

        return suppressed;
    }
}

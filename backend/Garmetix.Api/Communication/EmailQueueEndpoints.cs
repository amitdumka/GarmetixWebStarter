using Garmetix.Api.Auth;
using Garmetix.Core.Models.Communication;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Communication;

/// <summary>
/// Read-only queue/log visibility plus the permissioned actions the spec names: view timeline,
/// retry, cancel pending, reschedule, restore dead-letter. All gated behind CommunicationQueue
/// (Admin/PowerUser tier) - this surfaces every tenant's email traffic, not just the caller's own.
/// </summary>
public static class EmailQueueEndpoints
{
    private const int MaxPageSize = 200;

    public static RouteGroupBuilder MapEmailQueueEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/communication/queue")
            .WithTags("Communication & Mail - Queue")
            .RequireAuthorization(GarmetixPolicies.CommunicationQueue);

        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetDetailAsync);
        group.MapPost("/{id:guid}/retry", RetryAsync);
        group.MapPost("/{id:guid}/cancel", CancelAsync);
        group.MapPost("/{id:guid}/reschedule", RescheduleAsync);
        group.MapPost("/{id:guid}/restore", RestoreFromDeadLetterAsync);

        return group;
    }

    private static async Task<IResult> ListAsync(
        GarmetixDbContext db,
        string? status,
        string? sourceModule,
        string? correlationId,
        string? providerMessageId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 25 : pageSize, 1, MaxPageSize);

        var query = db.EmailQueueItems.AsNoTracking().Where(item => !item.Deleted);
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(item => item.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(sourceModule))
        {
            query = query.Where(item => item.SourceModule == sourceModule);
        }
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            query = query.Where(item => item.CorrelationId == correlationId);
        }
        if (!string.IsNullOrWhiteSpace(providerMessageId))
        {
            query = query.Where(item => item.ProviderMessageId == providerMessageId);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item.Subject.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var itemIds = items.Select(i => i.Id).ToArray();
        var recipientsByItem = await db.EmailRecipients.AsNoTracking()
            .Where(r => itemIds.Contains(r.QueueItemId))
            .ToListAsync(cancellationToken);
        var firstRecipientByItem = recipientsByItem.GroupBy(r => r.QueueItemId).ToDictionary(g => g.Key, g => g.First().EmailAddress);

        var result = items.Select(item => new EmailQueueItemSummaryDto(
            item.Id, item.Status, item.Subject, item.SourceModule, item.SourceType, item.SourceId,
            item.CorrelationId, item.ProviderMessageId, item.AttemptCount, item.NextAttemptAtUtc,
            item.LastErrorCode, item.CreatedAt, firstRecipientByItem.GetValueOrDefault(item.Id)));

        return Results.Ok(new { totalCount, page, pageSize, items = result });
    }

    private static async Task<IResult> GetDetailAsync(Guid id, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var item = await db.EmailQueueItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
        {
            return Results.NotFound(new { message = "Queue item not found." });
        }

        var recipients = await db.EmailRecipients.AsNoTracking().Where(r => r.QueueItemId == id).Select(r => r.EmailAddress).ToListAsync(cancellationToken);
        var attempts = await db.EmailDeliveryAttempts.AsNoTracking().Where(a => a.QueueItemId == id).OrderBy(a => a.AttemptNumber)
            .Select(a => new EmailDeliveryAttemptDto(a.AttemptNumber, a.StartedAtUtc, a.WasSuccess, a.ErrorCode, a.ErrorMessage)).ToListAsync(cancellationToken);
        var events = await db.EmailDeliveryEvents.AsNoTracking().Where(e => e.QueueItemId == id).OrderBy(e => e.OccurredAtUtc)
            .Select(e => new EmailDeliveryEventDto(e.EventType, e.OccurredAtUtc)).ToListAsync(cancellationToken);

        return Results.Ok(new EmailQueueItemDetailDto(
            item.Id, item.Status, item.Subject, item.HtmlBody, item.SourceModule, item.SourceType, item.SourceId,
            item.CorrelationId, item.IdempotencyKey, item.ProviderMessageId, item.AttemptCount, item.MaxAttempts,
            item.NextAttemptAtUtc, item.LastErrorCode, item.LastErrorMessage, item.CreatedAt, recipients, attempts, events));
    }

    private static async Task<IResult> RetryAsync(Guid id, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var item = await db.EmailQueueItems.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
        {
            return Results.NotFound(new { message = "Queue item not found." });
        }

        if (!EmailQueueStateMachine.CanTransition(item.Status, EmailCatalog.QueueStatuses.Pending))
        {
            return Results.BadRequest(new { message = $"Cannot retry a queue item in status '{item.Status}'." });
        }

        item.Status = EmailCatalog.QueueStatuses.Pending;
        item.NextAttemptAtUtc = null;
        item.LastErrorCode = null;
        item.LastErrorMessage = null;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { message = "Queue item scheduled for retry." });
    }

    private static async Task<IResult> CancelAsync(Guid id, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var item = await db.EmailQueueItems.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
        {
            return Results.NotFound(new { message = "Queue item not found." });
        }

        if (!EmailQueueStateMachine.CanTransition(item.Status, EmailCatalog.QueueStatuses.Cancelled))
        {
            return Results.BadRequest(new { message = $"Cannot cancel a queue item in status '{item.Status}'." });
        }

        item.Status = EmailCatalog.QueueStatuses.Cancelled;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { message = "Queue item cancelled." });
    }

    private static async Task<IResult> RescheduleAsync(Guid id, EmailQueueRescheduleRequest request, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var item = await db.EmailQueueItems.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
        {
            return Results.NotFound(new { message = "Queue item not found." });
        }

        if (item.Status is not (EmailCatalog.QueueStatuses.Pending or EmailCatalog.QueueStatuses.Deferred or EmailCatalog.QueueStatuses.Scheduled))
        {
            return Results.BadRequest(new { message = $"Cannot reschedule a queue item in status '{item.Status}'." });
        }

        item.Status = EmailCatalog.QueueStatuses.Scheduled;
        item.ScheduledForUtc = DateTime.SpecifyKind(request.ScheduledForUtc, DateTimeKind.Utc);
        item.NextAttemptAtUtc = null;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { message = "Queue item rescheduled." });
    }

    private static async Task<IResult> RestoreFromDeadLetterAsync(Guid id, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var item = await db.EmailQueueItems.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
        {
            return Results.NotFound(new { message = "Queue item not found." });
        }

        if (!EmailQueueStateMachine.CanTransition(item.Status, EmailCatalog.QueueStatuses.Pending))
        {
            return Results.BadRequest(new { message = "Only a DeadLetter item can be restored." });
        }

        item.Status = EmailCatalog.QueueStatuses.Pending;
        item.AttemptCount = 0;
        item.NextAttemptAtUtc = null;
        item.LastErrorCode = null;
        item.LastErrorMessage = null;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { message = "Queue item restored from dead-letter and re-queued." });
    }
}

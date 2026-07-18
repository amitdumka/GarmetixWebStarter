using System.Text.Json;
using Garmetix.Core.Models.Communication;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Communication;

/// <summary>
/// Brevo delivery-event webhook receiver. Deliberately NOT gated by GarmetixPolicies.Communication
/// (the caller is Brevo's server, not a logged-in Garmetix user) - authentication is
/// BrevoWebhookAuthenticator's Basic Auth/header-token check instead. Every event is
/// deduplicated on ProviderEventId (a deterministic key built from message-id+event+timestamp,
/// since Brevo's payload has no single guaranteed unique event id field) and stored as an
/// append-only EmailDeliveryEvent row. Confirmed hard bounce/invalid/complaint events create a
/// scoped EmailSuppressionEntry automatically.
/// </summary>
public static class BrevoWebhookEndpoints
{
    private static readonly Dictionary<string, string> EventTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sent"] = EmailCatalog.DeliveryEventTypes.Sent,
        ["delivered"] = EmailCatalog.DeliveryEventTypes.Delivered,
        ["deferred"] = EmailCatalog.DeliveryEventTypes.Deferred,
        ["soft bounce"] = EmailCatalog.DeliveryEventTypes.Deferred,
        ["hard bounce"] = EmailCatalog.DeliveryEventTypes.Bounced,
        ["bounce"] = EmailCatalog.DeliveryEventTypes.Bounced,
        ["blocked"] = EmailCatalog.DeliveryEventTypes.Blocked,
        ["invalid_email"] = EmailCatalog.DeliveryEventTypes.Invalid,
        ["invalid"] = EmailCatalog.DeliveryEventTypes.Invalid,
        ["complaint"] = EmailCatalog.DeliveryEventTypes.Complaint,
        ["spam"] = EmailCatalog.DeliveryEventTypes.Complaint,
        ["unsubscribed"] = EmailCatalog.DeliveryEventTypes.Unsubscribe,
        ["unsubscribe"] = EmailCatalog.DeliveryEventTypes.Unsubscribe,
        ["opened"] = EmailCatalog.DeliveryEventTypes.Opened,
        ["click"] = EmailCatalog.DeliveryEventTypes.Clicked,
        ["clicked"] = EmailCatalog.DeliveryEventTypes.Clicked,
    };

    private static readonly HashSet<string> SuppressionTriggerEventTypes =
    [
        EmailCatalog.DeliveryEventTypes.Bounced,
        EmailCatalog.DeliveryEventTypes.Invalid,
        EmailCatalog.DeliveryEventTypes.Complaint,
    ];

    public static RouteGroupBuilder MapBrevoWebhookEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/communication/webhooks");
        group.MapPost("/brevo", ReceiveBrevoWebhookAsync).RequireRateLimiting("brevo-webhook");
        return group;
    }

    private static async Task<IResult> ReceiveBrevoWebhookAsync(
        HttpContext context,
        GarmetixDbContext db,
        BrevoWebhookAuthenticator authenticator,
        Microsoft.Extensions.Options.IOptionsMonitor<BrevoWebhookOptions> optionsMonitor,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var auth = authenticator.Authenticate(context);
        if (!auth.IsAuthenticated)
        {
            logger.LogWarning("Rejected Brevo webhook call: {Reason}", auth.Reason);
            return Results.Unauthorized();
        }

        var maxBytes = optionsMonitor.CurrentValue.MaxBodyBytes;
        context.Request.EnableBuffering();
        if (context.Request.ContentLength is { } declaredLength && declaredLength > maxBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        string body;
        using (var reader = new StreamReader(context.Request.Body, leaveOpen: true))
        {
            body = await reader.ReadToEndAsync(cancellationToken);
        }
        if (body.Length > maxBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        List<BrevoWebhookEventPayload> events;
        try
        {
            events = ParsePayload(body);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { message = "Malformed webhook payload." });
        }

        var processed = 0;
        var skipped = 0;
        foreach (var payload in events)
        {
            var wasNew = await ProcessEventAsync(payload, db, cancellationToken);
            if (wasNew)
            {
                processed++;
            }
            else
            {
                skipped++;
            }
        }

        return Results.Ok(new { processed, skippedAsDuplicate = skipped });
    }

    private static List<BrevoWebhookEventPayload> ParsePayload(string body)
    {
        var trimmed = body.TrimStart();
        if (trimmed.StartsWith('['))
        {
            return JsonSerializer.Deserialize<List<BrevoWebhookEventPayload>>(body) ?? [];
        }

        var single = JsonSerializer.Deserialize<BrevoWebhookEventPayload>(body);
        return single is null ? [] : [single];
    }

    private static async Task<bool> ProcessEventAsync(BrevoWebhookEventPayload payload, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.Event))
        {
            return false;
        }

        var eventType = EventTypeMap.GetValueOrDefault(payload.Event.Trim(), payload.Event.Trim());
        var occurredAtUtc = ResolveOccurredAt(payload);
        var providerEventId = BuildProviderEventId(payload, eventType, occurredAtUtc);

        var alreadyExists = await db.EmailDeliveryEvents.AsNoTracking().AnyAsync(e => e.ProviderEventId == providerEventId, cancellationToken);
        if (alreadyExists)
        {
            return false;
        }

        var queueItem = string.IsNullOrWhiteSpace(payload.MessageId)
            ? null
            : await db.EmailQueueItems.FirstOrDefaultAsync(item => item.ProviderMessageId == payload.MessageId, cancellationToken);

        db.EmailDeliveryEvents.Add(new EmailDeliveryEvent
        {
            QueueItemId = queueItem?.Id,
            ProviderMessageId = payload.MessageId,
            EventType = eventType,
            ProviderEventId = providerEventId,
            OccurredAtUtc = occurredAtUtc,
            RawPayloadSanitizedJson = SanitizeForStorage(payload),
        });

        if (queueItem is not null)
        {
            ApplyStatusTransition(queueItem, eventType);
        }

        if (SuppressionTriggerEventTypes.Contains(eventType) && !string.IsNullOrWhiteSpace(payload.Email))
        {
            await UpsertSuppressionAsync(payload.Email.Trim().ToLowerInvariant(), eventType, queueItem?.CompanyId, db, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void ApplyStatusTransition(EmailQueueItem queueItem, string eventType)
    {
        var targetStatus = eventType switch
        {
            var t when t == EmailCatalog.DeliveryEventTypes.Delivered => EmailCatalog.QueueStatuses.Delivered,
            var t when t == EmailCatalog.DeliveryEventTypes.Bounced => EmailCatalog.QueueStatuses.Bounced,
            var t when t == EmailCatalog.DeliveryEventTypes.Complaint => EmailCatalog.QueueStatuses.Complained,
            var t when t == EmailCatalog.DeliveryEventTypes.Deferred => EmailCatalog.QueueStatuses.Deferred,
            _ => null,
        };

        // Opens/clicks/unsubscribe never drive queue status - "opens are not proof of human
        // reading" per the master prompt, and unsubscribe is a suppression concern, not a
        // delivery-status one.
        if (targetStatus is not null && EmailQueueStateMachine.CanTransition(queueItem.Status, targetStatus))
        {
            queueItem.Status = targetStatus;
            queueItem.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static async Task UpsertSuppressionAsync(string email, string eventType, Guid? companyId, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var existing = await db.EmailSuppressionEntries
            .FirstOrDefaultAsync(s => s.CompanyId == companyId && s.EmailAddress == email && s.IsActive, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        var reason = eventType switch
        {
            var t when t == EmailCatalog.DeliveryEventTypes.Bounced => EmailCatalog.SuppressionReasons.HardBounce,
            var t when t == EmailCatalog.DeliveryEventTypes.Invalid => EmailCatalog.SuppressionReasons.Invalid,
            var t when t == EmailCatalog.DeliveryEventTypes.Complaint => EmailCatalog.SuppressionReasons.Complaint,
            _ => EmailCatalog.SuppressionReasons.Manual,
        };

        db.EmailSuppressionEntries.Add(new EmailSuppressionEntry
        {
            CompanyId = companyId,
            EmailAddress = email,
            Reason = reason,
            IsActive = true,
        });
    }

    private static DateTime ResolveOccurredAt(BrevoWebhookEventPayload payload)
    {
        if (payload.TsEpochMs is { } ms)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
        }
        if (payload.TsSeconds is { } s)
        {
            return DateTimeOffset.FromUnixTimeSeconds(s).UtcDateTime;
        }
        if (!string.IsNullOrWhiteSpace(payload.Date) && DateTime.TryParse(payload.Date, out var parsed))
        {
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }
        return DateTime.UtcNow;
    }

    private static string BuildProviderEventId(BrevoWebhookEventPayload payload, string eventType, DateTime occurredAtUtc) =>
        $"{payload.MessageId}:{eventType}:{payload.Email}:{occurredAtUtc:O}";

    /// <summary>Stores only fields relevant to delivery diagnostics - never the full raw payload verbatim, keeping this append-only log free of anything beyond what's already modeled.</summary>
    private static string SanitizeForStorage(BrevoWebhookEventPayload payload) =>
        JsonSerializer.Serialize(new { payload.Event, payload.Email, payload.MessageId, payload.Reason, payload.Subject });
}

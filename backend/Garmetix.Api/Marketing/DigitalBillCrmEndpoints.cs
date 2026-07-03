using System.Text.Json;
using System.Text.RegularExpressions;
using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Models.Marketing;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Marketing;

public static class DigitalBillCrmEndpoints
{
    public static RouteGroupBuilder MapDigitalBillCrmEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/digital-bills")
            .WithTags("Digital Bill CRM")
            .RequireAuthorization(GarmetixPolicies.Marketing);

        admin.MapGet("/", ListDigitalBillsAsync);
        admin.MapPost("/sales/generate", GenerateForSaleFromRequestAsync);
        admin.MapPost("/sales/{invoiceId:guid}/generate", GenerateForSaleAsync);
        admin.MapPost("/sales/{invoiceKey}/generate", GenerateForSaleByKeyAsync);
        admin.MapPost("/{id:guid}/disable", DisableDigitalBillAsync).RequireAuthorization(GarmetixPolicies.Edit);
        admin.MapPost("/{id:guid}/regenerate-token", RegenerateTokenAsync).RequireAuthorization(GarmetixPolicies.Edit);
        admin.MapGet("/{id:guid}/events", GetDigitalBillEventsAsync);
        admin.MapGet("/{id:guid}/activity", GetDigitalBillActivityAsync);
        // Store managers can resend customer invoice links for their scoped store.
        // Salesmen/billers are intentionally excluded from the Marketing policy and use the Billing screen endpoints instead.
        admin.MapPost("/{id:guid}/send-whatsapp", SendDigitalBillWhatsAppAsync);
        admin.MapGet("/feedback", GetCustomerFeedbackAsync);

        var review = app.MapGroup("/api/digital-bill-review-settings")
            .WithTags("Digital Bill CRM")
            .RequireAuthorization(GarmetixPolicies.Marketing)
            .RequireAuthorization(GarmetixPolicies.Edit);
        review.MapGet("/", ListReviewSettingsAsync);
        review.MapPut("/store/{storeId:guid}", UpsertReviewSettingAsync).RequireAuthorization(GarmetixPolicies.Edit);

        var whatsappSettings = app.MapGroup("/api/digital-bill-whatsapp-settings")
            .WithTags("Digital Bill WhatsApp")
            .RequireAuthorization(GarmetixPolicies.Marketing)
            .RequireAuthorization(GarmetixPolicies.Edit);
        whatsappSettings.MapGet("/", ListWhatsAppSettingsAsync);
        whatsappSettings.MapPut("/store/{storeId:guid}", UpsertWhatsAppSettingAsync).RequireAuthorization(GarmetixPolicies.Edit);
        whatsappSettings.MapPost("/test-send", TestWhatsAppAsync).RequireAuthorization(GarmetixPolicies.Edit);

        var whatsappLogs = app.MapGroup("/api/digital-bill-whatsapp-logs")
            .WithTags("Digital Bill WhatsApp")
            .RequireAuthorization(GarmetixPolicies.Marketing);
        whatsappLogs.MapGet("/", ListWhatsAppLogsAsync);
        whatsappLogs.MapPost("/{id:guid}/retry", RetryWhatsAppLogAsync).RequireAuthorization(GarmetixPolicies.Edit);

        var banners = app.MapGroup("/api/invoice-ad-banners")
            .WithTags("Digital Bill CRM")
            .RequireAuthorization(GarmetixPolicies.Marketing)
            .RequireAuthorization(GarmetixPolicies.Edit);
        banners.MapGet("/", ListInvoiceAdBannersAsync);
        banners.MapPost("/", CreateInvoiceAdBannerAsync).RequireAuthorization(GarmetixPolicies.Edit);
        banners.MapPut("/{id:guid}", UpdateInvoiceAdBannerAsync).RequireAuthorization(GarmetixPolicies.Edit);
        banners.MapDelete("/{id:guid}", DeleteInvoiceAdBannerAsync).RequireAuthorization(GarmetixPolicies.Edit);

        app.MapGet("/api/digital-bill-analytics", GetDigitalBillAnalyticsAsync)
            .WithTags("Digital Bill CRM")
            .RequireAuthorization(GarmetixPolicies.Marketing);

        app.MapGet("/api/digital-bill-audiences", GetDigitalBillAudiencesAsync)
            .WithTags("Digital Bill CRM")
            .RequireAuthorization(GarmetixPolicies.Marketing)
            .RequireAuthorization(GarmetixPolicies.Edit);

        app.MapGet("/api/digital-bill-production-checks", GetDigitalBillProductionChecksAsync)
            .WithTags("Digital Bill CRM")
            .RequireAuthorization(GarmetixPolicies.Marketing)
            .RequireAuthorization(GarmetixPolicies.Edit);

        var campaigns = app.MapGroup("/api/digital-bill-campaigns")
            .WithTags("Digital Bill Campaigns")
            .RequireAuthorization(GarmetixPolicies.Marketing)
            .RequireAuthorization(GarmetixPolicies.Edit);
        campaigns.MapGet("/", ListDigitalBillCampaignsAsync);
        campaigns.MapGet("/{id:guid}", GetDigitalBillCampaignAsync);
        campaigns.MapGet("/{id:guid}/roi", GetDigitalBillCampaignRoiAsync);
        campaigns.MapPost("/preview", PreviewDigitalBillCampaignAsync).RequireAuthorization(GarmetixPolicies.Edit);
        campaigns.MapPost("/", CreateDigitalBillCampaignAsync).RequireAuthorization(GarmetixPolicies.Edit);
        campaigns.MapPost("/{id:guid}/queue", QueueDigitalBillCampaignAsync).RequireAuthorization(GarmetixPolicies.Edit);
        campaigns.MapPost("/{id:guid}/send-whatsapp", SendDigitalBillCampaignWhatsAppAsync).RequireAuthorization(GarmetixPolicies.Edit);
        campaigns.MapPost("/{id:guid}/mark-sent", MarkDigitalBillCampaignSentAsync).RequireAuthorization(GarmetixPolicies.Edit);
        campaigns.MapPost("/{id:guid}/cancel", CancelDigitalBillCampaignAsync).RequireAuthorization(GarmetixPolicies.Edit);

        var whatsAppWebhook = app.MapGroup("/api/public/digital-bill-whatsapp")
            .WithTags("Public Digital Bill WhatsApp")
            .AllowAnonymous();
        whatsAppWebhook.MapGet("/webhook/meta", VerifyMetaWhatsAppWebhookAsync);
        whatsAppWebhook.MapPost("/webhook/meta", ReceiveMetaWhatsAppWebhookAsync);

        var publicGroup = app.MapGroup("/api/public/digital-bills")
            .WithTags("Public Digital Bills")
            .AllowAnonymous();
        publicGroup.MapGet("/{token}/pdf", GetPublicDigitalBillPdfAsync);
        publicGroup.MapPost("/{token}/events", TrackPublicEventAsync);
        publicGroup.MapPost("/{token}/feedback", SubmitPublicFeedbackAsync);
        publicGroup.MapGet("/{token}", GetPublicDigitalBillAsync);

        return admin;
    }

    private static async Task<DigitalBillListResponseDto> ListDigitalBillsAsync(
        HttpContext context,
        GarmetixDbContext db,
        int page = 1,
        int pageSize = 50,
        string? q = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 200);
        var query = WorkspaceScope.ApplyTo(db.DigitalInvoices.AsNoTracking(), context)
            .Where(item => !item.Deleted);

        var term = q?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(item =>
                item.InvoiceNumber.ToLower().Contains(term) ||
                item.CustomerName.ToLower().Contains(term) ||
                item.CustomerMobile.ToLower().Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(item => item.InvoiceDate)
            .ThenByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new
            {
                item.Id,
                item.InvoiceId,
                item.InvoiceType,
                item.InvoiceNumber,
                item.InvoiceDate,
                item.CustomerName,
                item.CustomerMobile,
                item.Amount,
                item.PublicToken,
                item.ExpiresAt,
                item.IsActive,
                item.WhatsAppStatus,
                item.OpenCount,
                item.PdfDownloadCount,
                item.ReviewClickCount,
                item.FeedbackCount,
                item.CreatedAt,
                item.LastOpenedAt
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(item => new DigitalBillListItemDto(
                item.Id,
                item.InvoiceId,
                item.InvoiceType,
                item.InvoiceNumber,
                item.InvoiceDate,
                item.CustomerName,
                MaskMobile(item.CustomerMobile),
                item.Amount,
                item.PublicToken,
                $"/i/{Uri.EscapeDataString(item.PublicToken)}",
                item.ExpiresAt,
                item.IsActive,
                item.WhatsAppStatus,
                item.OpenCount,
                item.PdfDownloadCount,
                item.ReviewClickCount,
                item.FeedbackCount,
                item.CreatedAt,
                item.LastOpenedAt))
            .ToList();

        return new DigitalBillListResponseDto(items, total, page, pageSize);
    }

    private static async Task<IResult> GenerateForSaleFromRequestAsync(
        GenerateDigitalBillRequest request,
        DigitalBillCrmService service,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var key = request.InvoiceId?.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            key = request.InvoiceNumber?.Trim();
        }
        if (string.IsNullOrWhiteSpace(key))
        {
            key = request.InvoiceKey?.Trim();
        }

        return string.IsNullOrWhiteSpace(key)
            ? Results.BadRequest(new { message = "Sale invoice ID or invoice number is required." })
            : await GenerateForSaleKeyCoreAsync(key, service, context, db, cancellationToken);
    }

    private static async Task<IResult> GenerateForSaleAsync(
        Guid invoiceId,
        DigitalBillCrmService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var response = await service.EnsureForSaleInvoiceAsync(invoiceId, context, cancellationToken);
        return response is null
            ? Results.NotFound(new { message = "Sale invoice was not found in your workspace." })
            : Results.Ok(response);
    }

    private static async Task<IResult> GenerateForSaleByKeyAsync(
        string invoiceKey,
        DigitalBillCrmService service,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
        => await GenerateForSaleKeyCoreAsync(invoiceKey, service, context, db, cancellationToken);

    private static async Task<IResult> GenerateForSaleKeyCoreAsync(
        string invoiceKey,
        DigitalBillCrmService service,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var key = Uri.UnescapeDataString(invoiceKey).Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return Results.BadRequest(new { message = "Sale invoice ID or invoice number is required." });
        }

        if (Guid.TryParse(key, out var invoiceId))
        {
            return await GenerateForSaleAsync(invoiceId, service, context, cancellationToken);
        }

        var normalized = key.ToLowerInvariant();
        var resolvedInvoiceId = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.InvoiceNumber.ToLower() == normalized)
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (resolvedInvoiceId is null)
        {
            return Results.NotFound(new { message = $"Sale invoice '{key}' was not found in your workspace." });
        }

        return await GenerateForSaleAsync(resolvedInvoiceId.Value, service, context, cancellationToken);
    }

    private static async Task<IResult> DisableDigitalBillAsync(
        Guid id,
        DisableDigitalBillRequest request,
        DigitalBillCrmService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var response = await service.DisableAsync(id, request.Reason, context, cancellationToken);
        return response is null ? Results.NotFound() : Results.Ok(response);
    }

    private static async Task<IResult> RegenerateTokenAsync(
        Guid id,
        DigitalBillCrmService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var response = await service.RegenerateTokenAsync(id, context, cancellationToken);
        return response is null ? Results.NotFound() : Results.Ok(response);
    }

    private static async Task<IReadOnlyList<DigitalBillEventDto>> GetDigitalBillEventsAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var canSee = await WorkspaceScope.ApplyTo(db.DigitalInvoices.AsNoTracking(), context)
            .AnyAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (!canSee)
        {
            return Array.Empty<DigitalBillEventDto>();
        }

        return await db.DigitalInvoiceEvents.AsNoTracking()
            .Where(item => item.DigitalInvoiceId == id && !item.Deleted)
            .OrderByDescending(item => item.EventAt)
            .Take(100)
            .Select(item => new DigitalBillEventDto(
                item.Id,
                item.EventType,
                item.Source,
                item.TargetUrl,
                item.EventAt,
                item.IpAddress,
                item.UserAgent))
            .ToListAsync(cancellationToken);
    }


    private static async Task<IResult> GetDigitalBillActivityAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var item = await WorkspaceScope.ApplyTo(db.DigitalInvoices.AsNoTracking(), context)
            .Where(entity => entity.Id == id && !entity.Deleted)
            .Select(entity => new
            {
                entity.Id,
                entity.InvoiceId,
                entity.InvoiceType,
                entity.InvoiceNumber,
                entity.InvoiceDate,
                entity.CustomerName,
                entity.CustomerMobile,
                entity.Amount,
                entity.PublicToken,
                entity.ExpiresAt,
                entity.IsActive,
                entity.WhatsAppStatus,
                entity.OpenCount,
                entity.PdfDownloadCount,
                entity.ReviewClickCount,
                entity.FeedbackCount,
                entity.CreatedAt,
                entity.LastOpenedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
        {
            return Results.NotFound(new { message = "Digital bill was not found in your workspace." });
        }

        var summary = new DigitalBillListItemDto(
            item.Id,
            item.InvoiceId,
            item.InvoiceType,
            item.InvoiceNumber,
            item.InvoiceDate,
            item.CustomerName,
            MaskMobile(item.CustomerMobile),
            item.Amount,
            item.PublicToken,
            $"/i/{Uri.EscapeDataString(item.PublicToken)}",
            item.ExpiresAt,
            item.IsActive,
            item.WhatsAppStatus,
            item.OpenCount,
            item.PdfDownloadCount,
            item.ReviewClickCount,
            item.FeedbackCount,
            item.CreatedAt,
            item.LastOpenedAt);

        var events = await db.DigitalInvoiceEvents.AsNoTracking()
            .Where(entity => entity.DigitalInvoiceId == id && !entity.Deleted)
            .OrderByDescending(entity => entity.EventAt)
            .Take(200)
            .Select(entity => new DigitalBillEventDto(
                entity.Id,
                entity.EventType,
                entity.Source,
                entity.TargetUrl,
                entity.EventAt,
                entity.IpAddress,
                entity.UserAgent))
            .ToListAsync(cancellationToken);

        var whatsAppLogs = await db.WhatsAppMessageLogs.AsNoTracking()
            .Where(entity => entity.DigitalInvoiceId == id && !entity.Deleted)
            .OrderByDescending(entity => entity.CreatedAt)
            .Take(50)
            .Select(entity => new WhatsAppMessageLogDto(
                entity.Id,
                entity.DigitalInvoiceId,
                item.InvoiceNumber,
                MaskMobile(entity.CustomerMobile),
                entity.Provider,
                entity.TemplateName,
                entity.MessageBody,
                entity.Status,
                entity.ProviderMessageId,
                entity.ErrorMessage,
                entity.RetryCount,
                entity.CreatedAt,
                entity.SentAt,
                entity.DeliveredAt,
                entity.ReadAt))
            .ToListAsync(cancellationToken);

        var feedbackRows = await db.CustomerFeedback.AsNoTracking()
            .Where(entity => entity.DigitalInvoiceId == id && !entity.Deleted)
            .OrderByDescending(entity => entity.SubmittedAt)
            .Take(50)
            .Select(entity => new CustomerFeedbackDto(
                entity.Id,
                entity.DigitalInvoiceId,
                item.InvoiceNumber,
                entity.CustomerName,
                MaskMobile(entity.CustomerMobile),
                entity.Rating,
                entity.Message,
                entity.SubmittedAt))
            .ToListAsync(cancellationToken);

        var timeline = new List<DigitalBillTimelineItemDto>();
        timeline.Add(new DigitalBillTimelineItemDto(
            item.CreatedAt,
            "DigitalBillCreated",
            "Digital bill link created",
            $"Public link created for invoice {item.InvoiceNumber}.",
            "System",
            null,
            null));

        timeline.AddRange(events.Select(entity => new DigitalBillTimelineItemDto(
            entity.EventAt,
            entity.EventType,
            ToEventTitle(entity.EventType),
            ToEventDescription(entity),
            entity.Source,
            entity.TargetUrl,
            entity.UserAgent)));

        foreach (var log in whatsAppLogs)
        {
            timeline.Add(new DigitalBillTimelineItemDto(
                log.CreatedAt,
                "WhatsAppQueued",
                "WhatsApp message logged",
                $"Status: {log.Status}. Provider: {log.Provider}.",
                "WhatsApp",
                null,
                log.ErrorMessage));

            if (log.SentAt.HasValue)
            {
                timeline.Add(new DigitalBillTimelineItemDto(log.SentAt.Value, "WhatsAppSent", "WhatsApp sent", $"Message sent to {log.CustomerMobile}.", "WhatsApp", null, log.ProviderMessageId));
            }
            if (log.DeliveredAt.HasValue)
            {
                timeline.Add(new DigitalBillTimelineItemDto(log.DeliveredAt.Value, "WhatsAppDelivered", "WhatsApp delivered", $"Message delivered to {log.CustomerMobile}.", "WhatsApp", null, log.ProviderMessageId));
            }
            if (log.ReadAt.HasValue)
            {
                timeline.Add(new DigitalBillTimelineItemDto(log.ReadAt.Value, "WhatsAppRead", "WhatsApp read", $"Message read by {log.CustomerMobile}.", "WhatsApp", null, log.ProviderMessageId));
            }
        }

        timeline.AddRange(feedbackRows.Select(entity => new DigitalBillTimelineItemDto(
            entity.SubmittedAt,
            "FeedbackSubmitted",
            $"Feedback submitted · {entity.Rating}/5",
            string.IsNullOrWhiteSpace(entity.Message) ? "Customer submitted a rating without message." : entity.Message,
            "Feedback",
            null,
            null)));

        var orderedTimeline = timeline
            .OrderByDescending(entity => entity.At)
            .Take(300)
            .ToList();

        var totals = new DigitalBillActivityTotalsDto(
            item.OpenCount,
            item.PdfDownloadCount,
            item.ReviewClickCount,
            item.FeedbackCount,
            events.Count(entity => entity.EventType == "BannerClicked"),
            whatsAppLogs.Count,
            whatsAppLogs.Count(entity => entity.Status == "Sent" || entity.Status == "Delivered" || entity.Status == "Read"),
            whatsAppLogs.Count(entity => entity.Status == "Failed" || entity.Status == "Skipped" || entity.Status == "RetryLimitReached"));

        return Results.Ok(new DigitalBillActivityDto(summary, totals, orderedTimeline, events, whatsAppLogs, feedbackRows));
    }

    private static async Task<IReadOnlyList<CustomerFeedbackDto>> GetCustomerFeedbackAsync(
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var query = WorkspaceScope.ApplyTo(db.CustomerFeedback.AsNoTracking(), context)
            .Where(item => !item.Deleted)
            .Join(db.DigitalInvoices.AsNoTracking(),
                feedback => feedback.DigitalInvoiceId,
                digitalInvoice => digitalInvoice.Id,
                (feedback, digitalInvoice) => new { feedback, digitalInvoice });

        var rows = await query
            .OrderByDescending(item => item.feedback.SubmittedAt)
            .Take(200)
            .Select(item => new
            {
                item.feedback.Id,
                item.feedback.DigitalInvoiceId,
                item.digitalInvoice.InvoiceNumber,
                item.feedback.CustomerName,
                item.feedback.CustomerMobile,
                item.feedback.Rating,
                item.feedback.Message,
                item.feedback.SubmittedAt
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(item => new CustomerFeedbackDto(
                item.Id,
                item.DigitalInvoiceId,
                item.InvoiceNumber,
                item.CustomerName,
                MaskMobile(item.CustomerMobile),
                item.Rating,
                item.Message,
                item.SubmittedAt))
            .ToList();
    }

    private static async Task<IResult> GetPublicDigitalBillAsync(
        string token,
        DigitalBillCrmService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var response = await service.LoadPublicInvoiceAsync(token, context, cancellationToken);
        if (response is null)
        {
            return Results.NotFound(new { message = "Digital invoice link is invalid, disabled, or expired." });
        }

        context.Response.Headers.TryAdd("X-Robots-Tag", "noindex, nofollow");
        return Results.Ok(response);
    }

    private static async Task<IResult> GetPublicDigitalBillPdfAsync(
        string token,
        string? format,
        string? copy,
        bool? reprint,
        bool? signatures,
        DigitalBillCrmService service,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var pdf = await service.BuildPublicInvoicePdfAsync(token, context, format, copy, reprint, signatures, cancellationToken);
        if (pdf is null)
        {
            return Results.NotFound(new { message = "Digital invoice link is invalid, disabled, or expired." });
        }

        var invoiceNumber = await db.DigitalInvoices.AsNoTracking()
            .Where(item => item.PublicToken == token)
            .Select(item => item.InvoiceNumber)
            .FirstOrDefaultAsync(cancellationToken) ?? "invoice";
        var safeNumber = Regex.Replace(invoiceNumber, @"[^A-Za-z0-9_-]+", "-").Trim('-');
        context.Response.Headers.TryAdd("X-Robots-Tag", "noindex, nofollow");
        return Results.File(pdf, "application/pdf", $"{(safeNumber.Length > 0 ? safeNumber : "invoice")}-digital-bill.pdf");
    }

    private static async Task<IResult> TrackPublicEventAsync(
        string token,
        DigitalBillEventRequest request,
        DigitalBillCrmService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var ok = await service.TrackPublicEventAsync(token, request, context, cancellationToken);
        return ok ? Results.Ok(new { message = "Event tracked." }) : Results.NotFound();
    }

    private static async Task<IResult> SubmitPublicFeedbackAsync(
        string token,
        CustomerFeedbackSubmitRequest request,
        DigitalBillCrmService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (request.Rating < 1 || request.Rating > 5)
        {
            return Results.BadRequest(new { message = "Rating must be between 1 and 5." });
        }

        var response = await service.SubmitFeedbackAsync(token, request, context, cancellationToken);
        return response is null ? Results.NotFound() : Results.Ok(response);
    }

    private static async Task<IReadOnlyList<StoreReviewSettingDto>> ListReviewSettingsAsync(
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        return await WorkspaceScope.ApplyTo(db.StoreReviewSettings.AsNoTracking(), context)
            .Where(item => !item.Deleted)
            .OrderBy(item => item.StoreId)
            .Select(item => new StoreReviewSettingDto(
                item.Id,
                item.CompanyId,
                item.StoreGroupId,
                item.StoreId,
                item.GoogleReviewUrl,
                item.InstagramUrl,
                item.FacebookUrl,
                item.WhatsAppSupportNumber,
                item.EnableGoogleReview,
                item.EnableInstagram,
                item.EnableFacebook,
                item.EnableWhatsappSupport,
                item.EnablePrivateFeedback,
                item.ReviewButtonText,
                item.FeedbackButtonText))
            .ToListAsync(cancellationToken);
    }

    private static async Task<IResult> UpsertReviewSettingAsync(
        Guid storeId,
        StoreReviewSettingDto request,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var store = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == storeId && item.CompanyId == request.CompanyId, cancellationToken);
        if (store is null)
        {
            return Results.BadRequest(new { message = "Selected store is outside your workspace." });
        }

        var item = await db.StoreReviewSettings
            .FirstOrDefaultAsync(entity => entity.StoreId == storeId && entity.CompanyId == request.CompanyId && !entity.Deleted, cancellationToken);
        if (item is null)
        {
            item = new StoreReviewSetting
            {
                Id = Guid.NewGuid(),
                CompanyId = request.CompanyId,
                StoreGroupId = store.StoreGroupId,
                StoreId = storeId,
                CreatedBy = ResolveActor(context),
                CreatedAt = Now()
            };
            db.StoreReviewSettings.Add(item);
        }

        item.StoreGroupId = store.StoreGroupId;
        item.GoogleReviewUrl = CleanUrl(request.GoogleReviewUrl);
        item.InstagramUrl = CleanUrl(request.InstagramUrl);
        item.FacebookUrl = CleanUrl(request.FacebookUrl);
        item.WhatsAppSupportNumber = string.IsNullOrWhiteSpace(request.WhatsAppSupportNumber) ? null : request.WhatsAppSupportNumber.Trim();
        item.EnableGoogleReview = request.EnableGoogleReview;
        item.EnableInstagram = request.EnableInstagram;
        item.EnableFacebook = request.EnableFacebook;
        item.EnableWhatsappSupport = request.EnableWhatsappSupport;
        item.EnablePrivateFeedback = request.EnablePrivateFeedback;
        item.ReviewButtonText = string.IsNullOrWhiteSpace(request.ReviewButtonText) ? "Share your honest Google review" : request.ReviewButtonText.Trim();
        item.FeedbackButtonText = string.IsNullOrWhiteSpace(request.FeedbackButtonText) ? "Share private feedback" : request.FeedbackButtonText.Trim();
        item.UpdatedAt = Now();

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToReviewSettingDto(item));
    }

    private static StoreReviewSettingDto ToReviewSettingDto(StoreReviewSetting item)
        => new(
            item.Id,
            item.CompanyId,
            item.StoreGroupId,
            item.StoreId,
            item.GoogleReviewUrl,
            item.InstagramUrl,
            item.FacebookUrl,
            item.WhatsAppSupportNumber,
            item.EnableGoogleReview,
            item.EnableInstagram,
            item.EnableFacebook,
            item.EnableWhatsappSupport,
            item.EnablePrivateFeedback,
            item.ReviewButtonText,
            item.FeedbackButtonText);


    private static async Task<IResult> SendDigitalBillWhatsAppAsync(
        Guid id,
        SendDigitalBillWhatsAppRequest request,
        DigitalBillWhatsAppService whatsApp,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var response = await whatsApp.SendForDigitalInvoiceAsync(id, context, cancellationToken, force: request.Force);
        return response is null ? Results.NotFound(new { message = "Digital bill was not found in your workspace." }) : Results.Ok(response);
    }

    private static Task<IReadOnlyList<WhatsAppProviderSettingSafeDto>> ListWhatsAppSettingsAsync(
        DigitalBillWhatsAppService whatsApp,
        HttpContext context,
        CancellationToken cancellationToken)
        => whatsApp.ListSettingsAsync(context, cancellationToken);

    private static async Task<IResult> UpsertWhatsAppSettingAsync(
        Guid storeId,
        WhatsAppProviderSettingDto request,
        DigitalBillWhatsAppService whatsApp,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var response = await whatsApp.UpsertSettingAsync(storeId, request, context, cancellationToken);
        return response is null ? Results.BadRequest(new { message = "Selected store is outside your workspace." }) : Results.Ok(response);
    }

    private static async Task<IResult> TestWhatsAppAsync(
        WhatsAppTestSendRequest request,
        DigitalBillWhatsAppService whatsApp,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerMobile))
        {
            return Results.BadRequest(new { message = "Customer mobile is required for test send." });
        }

        var response = await whatsApp.TestSendAsync(request, context, cancellationToken);
        return response is null ? Results.BadRequest(new { message = "WhatsApp setting was not found for selected store." }) : Results.Ok(response);
    }

    private static async Task<WhatsAppMessageLogListResponseDto> ListWhatsAppLogsAsync(
        HttpContext context,
        GarmetixDbContext db,
        int page = 1,
        int pageSize = 50,
        string? status = null,
        string? q = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 200);

        var query = WorkspaceScope.ApplyTo(db.WhatsAppMessageLogs.AsNoTracking(), context)
            .Where(item => !item.Deleted);

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var normalized = status.Trim();
            query = query.Where(item => item.Status == normalized);
        }

        var term = q?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(item =>
                item.CustomerMobile.ToLower().Contains(term) ||
                item.Provider.ToLower().Contains(term) ||
                item.MessageBody.ToLower().Contains(term) ||
                (item.ErrorMessage != null && item.ErrorMessage.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var logs = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .GroupJoin(db.DigitalInvoices.AsNoTracking(),
                log => log.DigitalInvoiceId,
                digitalInvoice => (Guid?)digitalInvoice.Id,
                (log, invoices) => new { log, invoice = invoices.FirstOrDefault() })
            .Select(item => new WhatsAppMessageLogDto(
                item.log.Id,
                item.log.DigitalInvoiceId,
                item.invoice != null ? item.invoice.InvoiceNumber : null,
                MaskMobile(item.log.CustomerMobile),
                item.log.Provider,
                item.log.TemplateName,
                item.log.MessageBody,
                item.log.Status,
                item.log.ProviderMessageId,
                item.log.ErrorMessage,
                item.log.RetryCount,
                item.log.CreatedAt,
                item.log.SentAt,
                item.log.DeliveredAt,
                item.log.ReadAt))
            .ToListAsync(cancellationToken);

        return new WhatsAppMessageLogListResponseDto(logs, total, page, pageSize);
    }

    private static async Task<IResult> RetryWhatsAppLogAsync(
        Guid id,
        DigitalBillWhatsAppService whatsApp,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var response = await whatsApp.RetryLogAsync(id, context, cancellationToken);
        return response is null ? Results.NotFound(new { message = "WhatsApp log was not found in your workspace." }) : Results.Ok(response);
    }


    private static async Task<InvoiceAdBannerListResponseDto> ListInvoiceAdBannersAsync(
        HttpContext context,
        GarmetixDbContext db,
        int page = 1,
        int pageSize = 50,
        string? q = null,
        string? position = null,
        bool? active = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 200);
        var query = WorkspaceScope.ApplyTo(db.InvoiceAdBanners.AsNoTracking(), context)
            .Where(item => !item.Deleted);

        var term = q?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(item =>
                item.Title.ToLower().Contains(term) ||
                item.ImageUrl.ToLower().Contains(term) ||
                (item.TargetUrl != null && item.TargetUrl.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(position) && !position.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedPosition = NormalizeBannerPosition(position);
            query = query.Where(item => item.Position == normalizedPosition);
        }

        if (active.HasValue)
        {
            query = query.Where(item => item.IsActive == active.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(item => item.IsActive)
            .ThenByDescending(item => item.Priority)
            .ThenByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .GroupJoin(db.Stores.AsNoTracking(),
                banner => banner.StoreId,
                store => (Guid?)store.Id,
                (banner, stores) => new { banner, store = stores.FirstOrDefault() })
            .Select(item => new InvoiceAdBannerDto(
                item.banner.Id,
                item.banner.CompanyId,
                item.banner.StoreGroupId,
                item.banner.StoreId,
                item.store != null ? item.store.Name : null,
                item.banner.Title,
                item.banner.ImageUrl,
                item.banner.TargetUrl,
                item.banner.Position,
                item.banner.StartDate,
                item.banner.EndDate,
                item.banner.IsActive,
                item.banner.Priority,
                item.banner.ClickCount,
                item.banner.CreatedAt,
                item.banner.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new InvoiceAdBannerListResponseDto(rows, total, page, pageSize);
    }

    private static async Task<IResult> CreateInvoiceAdBannerAsync(
        InvoiceAdBannerUpsertRequest request,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var validation = await ResolveBannerScopeAsync(request, context, db, cancellationToken);
        if (validation.Error is not null)
        {
            return Results.BadRequest(new { message = validation.Error });
        }

        var item = new InvoiceAdBanner
        {
            Id = Guid.NewGuid(),
            CompanyId = validation.CompanyId,
            StoreGroupId = validation.StoreGroupId,
            StoreId = validation.StoreId,
            CreatedAt = Now(),
            CreatedBy = ResolveActor(context)
        };
        ApplyBannerFields(item, request);
        db.InvoiceAdBanners.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await ToInvoiceAdBannerDtoAsync(item.Id, context, db, cancellationToken));
    }

    private static async Task<IResult> UpdateInvoiceAdBannerAsync(
        Guid id,
        InvoiceAdBannerUpsertRequest request,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var item = await WorkspaceScope.ApplyTo(db.InvoiceAdBanners, context)
            .FirstOrDefaultAsync(entity => entity.Id == id && !entity.Deleted, cancellationToken);
        if (item is null)
        {
            return Results.NotFound(new { message = "Ad banner was not found in your workspace." });
        }

        var validation = await ResolveBannerScopeAsync(request, context, db, cancellationToken);
        if (validation.Error is not null)
        {
            return Results.BadRequest(new { message = validation.Error });
        }

        item.CompanyId = validation.CompanyId;
        item.StoreGroupId = validation.StoreGroupId;
        item.StoreId = validation.StoreId;
        ApplyBannerFields(item, request);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await ToInvoiceAdBannerDtoAsync(item.Id, context, db, cancellationToken));
    }

    private static async Task<IResult> DeleteInvoiceAdBannerAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var item = await WorkspaceScope.ApplyTo(db.InvoiceAdBanners, context)
            .FirstOrDefaultAsync(entity => entity.Id == id && !entity.Deleted, cancellationToken);
        if (item is null)
        {
            return Results.NotFound(new { message = "Ad banner was not found in your workspace." });
        }

        item.Deleted = true;
        item.IsActive = false;
        item.UpdatedAt = Now();
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { message = "Ad banner deleted." });
    }

    private static async Task<IResult> GetDigitalBillAnalyticsAsync(
        HttpContext context,
        GarmetixDbContext db,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Guid? storeId = null,
        CancellationToken cancellationToken = default)
    {
        var to = (toDate ?? Now()).Date.AddDays(1).AddTicks(-1);
        var from = (fromDate ?? to.Date.AddDays(-29)).Date;

        var billQuery = WorkspaceScope.ApplyTo(db.DigitalInvoices.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.InvoiceDate >= from && item.InvoiceDate <= to);
        var eventQuery = WorkspaceScope.ApplyTo(db.DigitalInvoiceEvents.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.EventAt >= from && item.EventAt <= to);
        var feedbackQuery = WorkspaceScope.ApplyTo(db.CustomerFeedback.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.SubmittedAt >= from && item.SubmittedAt <= to);
        var whatsappQuery = WorkspaceScope.ApplyTo(db.WhatsAppMessageLogs.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.CreatedAt >= from && item.CreatedAt <= to);

        if (storeId.HasValue)
        {
            billQuery = billQuery.Where(item => item.StoreId == storeId.Value);
            eventQuery = eventQuery.Where(item => item.StoreId == storeId.Value);
            feedbackQuery = feedbackQuery.Where(item => item.StoreId == storeId.Value);
            whatsappQuery = whatsappQuery.Where(item => item.StoreId == storeId.Value);
        }

        var billRows = await billQuery
            .GroupBy(item => item.InvoiceDate.Date)
            .Select(group => new { Date = group.Key, Bills = group.Count(), Active = group.Count(item => item.IsActive), Opens = group.Sum(item => item.OpenCount), Pdfs = group.Sum(item => item.PdfDownloadCount), Reviews = group.Sum(item => item.ReviewClickCount), Feedbacks = group.Sum(item => item.FeedbackCount) })
            .ToListAsync(cancellationToken);
        var eventRows = await eventQuery
            .GroupBy(item => new { Date = item.EventAt.Date, item.EventType })
            .Select(group => new { group.Key.Date, group.Key.EventType, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var feedbackRows = await feedbackQuery
            .GroupBy(item => item.SubmittedAt.Date)
            .Select(group => new { Date = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var whatsappRows = await whatsappQuery
            .GroupBy(item => new { Date = item.CreatedAt.Date, item.Status })
            .Select(group => new { group.Key.Date, group.Key.Status, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var totalBills = billRows.Sum(item => item.Bills);
        var activeBills = billRows.Sum(item => item.Active);
        var openCount = billRows.Sum(item => item.Opens);
        var pdfCount = billRows.Sum(item => item.Pdfs);
        var reviewCount = billRows.Sum(item => item.Reviews);
        var feedbackCount = Math.Max(billRows.Sum(item => item.Feedbacks), feedbackRows.Sum(item => item.Count));
        var bannerClickCount = eventRows.Where(item => item.EventType == "BannerClicked").Sum(item => item.Count);
        var sentCount = whatsappRows.Where(item => item.Status == "Sent" || item.Status == "Delivered" || item.Status == "Read").Sum(item => item.Count);
        var deliveredCount = whatsappRows.Where(item => item.Status == "Delivered" || item.Status == "Read").Sum(item => item.Count);
        var readCount = whatsappRows.Where(item => item.Status == "Read").Sum(item => item.Count);
        var failedCount = whatsappRows.Where(item => item.Status == "Failed" || item.Status == "Skipped" || item.Status == "RetryLimitReached").Sum(item => item.Count);

        var days = Enumerable.Range(0, Math.Max(1, (to.Date - from.Date).Days + 1))
            .Select(offset => from.Date.AddDays(offset))
            .Select(date =>
            {
                var bill = billRows.FirstOrDefault(item => item.Date == date);
                var events = eventRows.Where(item => item.Date == date).ToList();
                var whatsapps = whatsappRows.Where(item => item.Date == date).ToList();
                return new DigitalBillAnalyticsDayDto(
                    date,
                    bill?.Bills ?? 0,
                    bill?.Opens ?? events.Where(item => item.EventType == "Opened").Sum(item => item.Count),
                    bill?.Pdfs ?? events.Where(item => item.EventType == "PdfDownloaded").Sum(item => item.Count),
                    bill?.Reviews ?? events.Where(item => item.EventType == "ReviewClicked").Sum(item => item.Count),
                    feedbackRows.FirstOrDefault(item => item.Date == date)?.Count ?? bill?.Feedbacks ?? 0,
                    events.Where(item => item.EventType == "BannerClicked").Sum(item => item.Count),
                    whatsapps.Where(item => item.Status == "Sent" || item.Status == "Delivered" || item.Status == "Read").Sum(item => item.Count),
                    whatsapps.Where(item => item.Status == "Delivered" || item.Status == "Read").Sum(item => item.Count),
                    whatsapps.Where(item => item.Status == "Read").Sum(item => item.Count),
                    whatsapps.Where(item => item.Status == "Failed" || item.Status == "Skipped" || item.Status == "RetryLimitReached").Sum(item => item.Count));
            })
            .ToList();

        return Results.Ok(new DigitalBillAnalyticsDto(from, to, totalBills, activeBills, openCount, pdfCount, reviewCount, feedbackCount, bannerClickCount, sentCount, deliveredCount, readCount, failedCount, days));
    }



    private static async Task<IResult> GetDigitalBillAudiencesAsync(
        HttpContext context,
        GarmetixDbContext db,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Guid? storeId = null,
        string? segment = null,
        string? q = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 200);
        const int maxRows = 5000;
        var requestedSegment = NormalizeAudienceSegment(segment);
        var to = (toDate ?? Now()).Date.AddDays(1).AddTicks(-1);
        var from = (fromDate ?? to.Date.AddDays(-29)).Date;

        var billQuery = WorkspaceScope.ApplyTo(db.DigitalInvoices.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.InvoiceDate >= from && item.InvoiceDate <= to);

        if (storeId.HasValue)
        {
            billQuery = billQuery.Where(item => item.StoreId == storeId.Value);
        }

        var term = q?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(term))
        {
            billQuery = billQuery.Where(item =>
                item.InvoiceNumber.ToLower().Contains(term) ||
                item.CustomerName.ToLower().Contains(term) ||
                item.CustomerMobile.ToLower().Contains(term));
        }

        var invoiceRows = await (
                from bill in billQuery
                join store in db.Stores.AsNoTracking() on bill.StoreId equals store.Id into stores
                from store in stores.DefaultIfEmpty()
                orderby bill.InvoiceDate descending, bill.CreatedAt descending
                select new
                {
                    bill.Id,
                    bill.CompanyId,
                    bill.StoreGroupId,
                    bill.StoreId,
                    StoreName = store != null ? store.Name : string.Empty,
                    bill.InvoiceNumber,
                    bill.InvoiceDate,
                    bill.CustomerName,
                    bill.CustomerMobile,
                    bill.Amount,
                    bill.PublicToken,
                    bill.OpenCount,
                    bill.PdfDownloadCount,
                    bill.ReviewClickCount,
                    bill.FeedbackCount,
                    bill.WhatsAppStatus,
                    bill.CreatedAt,
                    bill.LastOpenedAt,
                    bill.LastWhatsAppSentAt
                })
            .Take(maxRows + 1)
            .ToListAsync(cancellationToken);

        var isTruncated = invoiceRows.Count > maxRows;
        if (isTruncated)
        {
            invoiceRows = invoiceRows.Take(maxRows).ToList();
        }

        var invoiceIds = invoiceRows.Select(item => item.Id).ToList();
        List<AudienceEventRow> eventRows = invoiceIds.Count == 0
            ? new()
            : await db.DigitalInvoiceEvents.AsNoTracking()
                .Where(item => invoiceIds.Contains(item.DigitalInvoiceId) && !item.Deleted)
                .Select(item => new AudienceEventRow(item.DigitalInvoiceId, item.EventType, item.EventAt))
                .ToListAsync(cancellationToken);
        List<AudienceFeedbackRow> feedbackRows = invoiceIds.Count == 0
            ? new()
            : await db.CustomerFeedback.AsNoTracking()
                .Where(item => invoiceIds.Contains(item.DigitalInvoiceId) && !item.Deleted)
                .Select(item => new AudienceFeedbackRow(item.DigitalInvoiceId, item.Rating, item.SubmittedAt))
                .ToListAsync(cancellationToken);
        List<AudienceWhatsAppRow> whatsappRows = invoiceIds.Count == 0
            ? new()
            : await db.WhatsAppMessageLogs.AsNoTracking()
                .Where(item => item.DigitalInvoiceId.HasValue && invoiceIds.Contains(item.DigitalInvoiceId.Value) && !item.Deleted)
                .Select(item => new AudienceWhatsAppRow(item.DigitalInvoiceId.Value, item.Status, item.CreatedAt, item.SentAt, item.DeliveredAt, item.ReadAt))
                .ToListAsync(cancellationToken);

        var eventLookup = eventRows.GroupBy(item => item.DigitalInvoiceId).ToDictionary(group => group.Key, group => group.ToList());
        var feedbackLookup = feedbackRows.GroupBy(item => item.DigitalInvoiceId).ToDictionary(group => group.Key, group => group.ToList());
        var whatsappLookup = whatsappRows.GroupBy(item => item.DigitalInvoiceId).ToDictionary(group => group.Key, group => group.ToList());

        var audience = invoiceRows
            .GroupBy(item => BuildAudienceKey(item.CustomerMobile, item.CustomerName, item.Id))
            .Select(group =>
            {
                var invoices = group
                    .OrderByDescending(item => item.InvoiceDate)
                    .ThenByDescending(item => item.CreatedAt)
                    .ToList();
                var last = invoices[0];
                var groupEvents = invoices.SelectMany(item => eventLookup.TryGetValue(item.Id, out var rows) ? rows : Enumerable.Empty<AudienceEventRow>()).ToList();
                var groupFeedback = invoices.SelectMany(item => feedbackLookup.TryGetValue(item.Id, out var rows) ? rows : Enumerable.Empty<AudienceFeedbackRow>()).ToList();
                var groupWhatsApp = invoices.SelectMany(item => whatsappLookup.TryGetValue(item.Id, out var rows) ? rows : Enumerable.Empty<AudienceWhatsAppRow>()).ToList();
                var openCount = invoices.Sum(item => item.OpenCount);
                var pdfCount = invoices.Sum(item => item.PdfDownloadCount);
                var reviewCount = invoices.Sum(item => item.ReviewClickCount);
                var feedbackCount = Math.Max(invoices.Sum(item => item.FeedbackCount), groupFeedback.Count);
                var bannerClickCount = groupEvents.Count(item => item.EventType == "BannerClicked");
                var sentCount = groupWhatsApp.Count(item => IsWhatsAppSuccessful(item.Status));
                var failedCount = groupWhatsApp.Count(item => IsWhatsAppFailed(item.Status))
                    + invoices.Count(item => IsWhatsAppFailed(item.WhatsAppStatus));
                var lastFeedback = groupFeedback.OrderByDescending(item => item.SubmittedAt).FirstOrDefault();
                var worstFeedback = groupFeedback.Count == 0 ? null : groupFeedback.Min(item => (int?)item.Rating);
                var lastActivityCandidates = new List<DateTime?>
                {
                    last.LastOpenedAt,
                    last.LastWhatsAppSentAt,
                    last.CreatedAt,
                    groupEvents.Count == 0 ? null : groupEvents.Max(item => (DateTime?)item.EventAt),
                    groupFeedback.Count == 0 ? null : groupFeedback.Max(item => (DateTime?)item.SubmittedAt),
                    groupWhatsApp.Count == 0 ? null : groupWhatsApp.Max(item => (DateTime?)LatestWhatsAppActivityAt(item.CreatedAt, item.SentAt, item.DeliveredAt, item.ReadAt))
                };
                var lastActivityAt = lastActivityCandidates.Where(item => item.HasValue).Max();
                var recommendation = BuildAudienceRecommendation(last.CustomerMobile, openCount, reviewCount, feedbackCount, worstFeedback, failedCount, last.WhatsAppStatus, sentCount);

                return new DigitalBillAudienceItemDto(
                    group.Key,
                    string.IsNullOrWhiteSpace(last.CustomerName) ? "Walk-in Customer" : last.CustomerName,
                    last.CustomerMobile,
                    last.StoreId,
                    string.IsNullOrWhiteSpace(last.StoreName) ? "Store" : last.StoreName,
                    last.Id,
                    last.InvoiceNumber,
                    last.InvoiceDate,
                    $"/i/{Uri.EscapeDataString(last.PublicToken)}",
                    invoices.Count,
                    invoices.Sum(item => item.Amount),
                    openCount,
                    pdfCount,
                    reviewCount,
                    feedbackCount,
                    bannerClickCount,
                    sentCount,
                    failedCount,
                    last.WhatsAppStatus,
                    lastFeedback?.Rating,
                    worstFeedback,
                    lastActivityAt,
                    recommendation);
            })
            .ToList();

        var summaries = BuildAudienceSummaries(audience);
        var filteredAudience = audience
            .Where(item => MatchesAudienceSegment(item, requestedSegment))
            .OrderByDescending(item => item.LastActivityAt ?? item.LastInvoiceDate)
            .ThenBy(item => item.CustomerName)
            .ToList();

        var total = filteredAudience.Count;
        var items = filteredAudience
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Results.Ok(new DigitalBillAudienceResponseDto(
            items,
            summaries,
            total,
            page,
            pageSize,
            requestedSegment,
            from,
            to,
            isTruncated,
            Now()));
    }

    private static async Task<IResult> VerifyMetaWhatsAppWebhookAsync(HttpContext context, IConfiguration configuration)
    {
        var query = context.Request.Query;
        var mode = query["hub.mode"].FirstOrDefault();
        var token = query["hub.verify_token"].FirstOrDefault();
        var challenge = query["hub.challenge"].FirstOrDefault();
        var expected = configuration["WhatsApp:MetaWebhookVerifyToken"] ?? configuration["DigitalBillWhatsApp:MetaWebhookVerifyToken"];

        if (string.Equals(mode, "subscribe", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(challenge)
            && !string.IsNullOrWhiteSpace(expected)
            && string.Equals(token, expected, StringComparison.Ordinal))
        {
            return Results.Text(challenge, "text/plain");
        }

        return Results.Unauthorized();
    }

    private static async Task<IResult> ReceiveMetaWhatsAppWebhookAsync(
        JsonElement payload,
        DigitalBillWhatsAppService whatsApp,
        CancellationToken cancellationToken)
    {
        var result = await whatsApp.HandleMetaWebhookAsync(payload, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<InvoiceAdBannerDto?> ToInvoiceAdBannerDtoAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
        => await WorkspaceScope.ApplyTo(db.InvoiceAdBanners.AsNoTracking(), context)
            .Where(item => item.Id == id && !item.Deleted)
            .GroupJoin(db.Stores.AsNoTracking(),
                banner => banner.StoreId,
                store => (Guid?)store.Id,
                (banner, stores) => new { banner, store = stores.FirstOrDefault() })
            .Select(item => new InvoiceAdBannerDto(
                item.banner.Id,
                item.banner.CompanyId,
                item.banner.StoreGroupId,
                item.banner.StoreId,
                item.store != null ? item.store.Name : null,
                item.banner.Title,
                item.banner.ImageUrl,
                item.banner.TargetUrl,
                item.banner.Position,
                item.banner.StartDate,
                item.banner.EndDate,
                item.banner.IsActive,
                item.banner.Priority,
                item.banner.ClickCount,
                item.banner.CreatedAt,
                item.banner.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

    private static async Task<(Guid CompanyId, Guid? StoreGroupId, Guid? StoreId, string? Error)> ResolveBannerScopeAsync(
        InvoiceAdBannerUpsertRequest request,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        if (request.StoreId.HasValue)
        {
            var store = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
                .Where(item => item.Id == request.StoreId.Value)
                .Select(item => new { item.Id, item.CompanyId, item.StoreGroupId })
                .FirstOrDefaultAsync(cancellationToken);
            return store is null
                ? (Guid.Empty, null, null, "Selected store is outside your workspace.")
                : (store.CompanyId, store.StoreGroupId, store.Id, null);
        }

        var requestedCompanyId = request.CompanyId ?? WorkspaceScope.ClaimGuid(context, "companyId");
        if (!requestedCompanyId.HasValue || requestedCompanyId.Value == Guid.Empty)
        {
            var fallback = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
                .Select(item => new { item.CompanyId, item.StoreGroupId })
                .FirstOrDefaultAsync(cancellationToken);
            if (fallback is null)
            {
                return (Guid.Empty, null, null, "Company or store is required for company-wide banners.");
            }
            requestedCompanyId = fallback.CompanyId;
        }

        var companyAllowed = await WorkspaceScope.ApplyTo(db.Companies.AsNoTracking(), context)
            .AnyAsync(item => item.Id == requestedCompanyId.Value && !item.Deleted, cancellationToken);
        if (!companyAllowed)
        {
            return (Guid.Empty, null, null, "Selected company is outside your workspace.");
        }

        Guid? storeGroupId = request.StoreGroupId;
        if (storeGroupId.HasValue)
        {
            var groupAllowed = await WorkspaceScope.ApplyTo(db.StoreGroups.AsNoTracking(), context)
                .AnyAsync(item => item.Id == storeGroupId.Value && item.CompanyId == requestedCompanyId.Value && !item.Deleted, cancellationToken);
            if (!groupAllowed)
            {
                return (Guid.Empty, null, null, "Selected store group is outside your workspace.");
            }
        }

        return (requestedCompanyId.Value, storeGroupId, null, null);
    }

    private static void ApplyBannerFields(InvoiceAdBanner item, InvoiceAdBannerUpsertRequest request)
    {
        item.Title = string.IsNullOrWhiteSpace(request.Title) ? "Untitled banner" : request.Title.Trim();
        item.ImageUrl = CleanUrl(request.ImageUrl) ?? string.Empty;
        item.TargetUrl = CleanUrl(request.TargetUrl);
        item.Position = NormalizeBannerPosition(request.Position);
        item.StartDate = request.StartDate;
        item.EndDate = request.EndDate;
        item.IsActive = request.IsActive;
        item.Priority = Math.Clamp(request.Priority, 0, 9999);
        item.UpdatedAt = Now();
    }

    private static string NormalizeBannerPosition(string? value)
    {
        var normalized = Regex.Replace(value ?? string.Empty, @"[^A-Za-z]", string.Empty);
        return normalized.Equals("Header", StringComparison.OrdinalIgnoreCase) ? "Header"
            : normalized.Equals("Bottom", StringComparison.OrdinalIgnoreCase) ? "Bottom"
            : "Footer";
    }


    private static string ToEventTitle(string? eventType)
        => eventType switch
        {
            "Opened" => "Customer opened invoice",
            "PdfDownloaded" => "PDF downloaded",
            "ReviewClicked" => "Google review clicked",
            "InstagramClicked" => "Instagram clicked",
            "FacebookClicked" => "Facebook clicked",
            "WhatsappSupportClicked" => "WhatsApp support clicked",
            "BannerClicked" => "Marketing banner clicked",
            "FeedbackSubmitted" => "Feedback submitted",
            _ => string.IsNullOrWhiteSpace(eventType) ? "Digital bill event" : eventType
        };

    private static string ToEventDescription(DigitalBillEventDto entity)
    {
        var target = string.IsNullOrWhiteSpace(entity.TargetUrl) ? null : $" Target: {entity.TargetUrl}";
        return entity.EventType switch
        {
            "Opened" => "Customer opened the digital invoice page.",
            "PdfDownloaded" => "Customer downloaded the invoice PDF.",
            "ReviewClicked" => "Customer clicked the Google review button.",
            "InstagramClicked" => "Customer clicked the Instagram button.",
            "FacebookClicked" => "Customer clicked the Facebook button.",
            "WhatsappSupportClicked" => "Customer clicked WhatsApp support.",
            "BannerClicked" => $"Customer clicked a marketing banner.{target}",
            _ => $"Event recorded from {entity.Source ?? "digital bill page"}.{target}"
        };
    }




    private static async Task<DigitalBillProductionReadinessDto> GetDigitalBillProductionChecksAsync(
        HttpContext context,
        GarmetixDbContext db,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var digitalBillQuery = WorkspaceScope.ApplyTo(db.DigitalInvoices.AsNoTracking(), context)
            .Where(item => !item.Deleted);
        var whatsappQuery = WorkspaceScope.ApplyTo(db.WhatsAppProviderSettings.AsNoTracking(), context)
            .Where(item => !item.Deleted);
        var reviewQuery = WorkspaceScope.ApplyTo(db.StoreReviewSettings.AsNoTracking(), context)
            .Where(item => !item.Deleted);
        var bannerQuery = WorkspaceScope.ApplyTo(db.InvoiceAdBanners.AsNoTracking(), context)
            .Where(item => !item.Deleted);
        var campaignQuery = WorkspaceScope.ApplyTo(db.DigitalBillCampaigns.AsNoTracking(), context)
            .Where(item => !item.Deleted);
        var feedbackQuery = WorkspaceScope.ApplyTo(db.CustomerFeedback.AsNoTracking(), context)
            .Where(item => !item.Deleted);

        var publicBaseUrl = configuration["DigitalBills:PublicBaseUrl"]?.Trim();
        var webhookToken = configuration["WhatsApp:MetaWebhookVerifyToken"]?.Trim();
        var digitalBillCount = await digitalBillQuery.CountAsync(cancellationToken);
        var activeDigitalBillCount = await digitalBillQuery.CountAsync(item => item.IsActive, cancellationToken);
        var whatsappSettingsCount = await whatsappQuery.CountAsync(cancellationToken);
        var enabledWhatsAppSettingsCount = await whatsappQuery.CountAsync(item => item.IsEnabled, cancellationToken);
        var metaWhatsAppSettingsCount = await whatsappQuery.CountAsync(item => item.IsEnabled && item.Provider == "MetaCloudApi" && item.ApiToken != null && item.PhoneNumberId != null, cancellationToken);
        var reviewSettingsCount = await reviewQuery.CountAsync(item => item.EnableGoogleReview || item.EnablePrivateFeedback || item.EnableInstagram, cancellationToken);
        var activeBannerCount = await bannerQuery.CountAsync(item => item.IsActive, cancellationToken);
        var campaignCount = await campaignQuery.CountAsync(cancellationToken);
        var feedbackCount = await feedbackQuery.CountAsync(cancellationToken);

        var checks = new List<DigitalBillProductionCheckDto>
        {
            new(
                "DIGITAL_BILLS_PRESENT",
                "Digital bill records",
                digitalBillCount > 0 ? "Passed" : "Warning",
                digitalBillCount > 0 ? $"{digitalBillCount} digital bill(s) found." : "No digital bills found yet. Create one sale invoice and generate a digital bill link."),
            new(
                "PUBLIC_BASE_URL",
                "Public base URL",
                !string.IsNullOrWhiteSpace(publicBaseUrl) ? "Passed" : "Warning",
                !string.IsNullOrWhiteSpace(publicBaseUrl) ? $"DigitalBills:PublicBaseUrl is set to {publicBaseUrl}." : "DigitalBills:PublicBaseUrl is not set. WhatsApp messages may use the current request host instead of your production domain."),
            new(
                "REVIEW_SETTINGS",
                "Review/feedback settings",
                reviewSettingsCount > 0 ? "Passed" : "Warning",
                reviewSettingsCount > 0 ? $"{reviewSettingsCount} review setting row(s) configured." : "Configure Google review/private feedback settings for stores."),
            new(
                "WHATSAPP_SETTINGS",
                "WhatsApp provider settings",
                enabledWhatsAppSettingsCount > 0 ? "Passed" : "Warning",
                enabledWhatsAppSettingsCount > 0 ? $"{enabledWhatsAppSettingsCount} enabled WhatsApp setting row(s), {metaWhatsAppSettingsCount} Meta Cloud ready." : "No enabled WhatsApp provider setting found. Manual copy/export will still work."),
            new(
                "WHATSAPP_WEBHOOK_TOKEN",
                "WhatsApp webhook verify token",
                !string.IsNullOrWhiteSpace(webhookToken) ? "Passed" : "Warning",
                !string.IsNullOrWhiteSpace(webhookToken) ? "Meta webhook verify token is configured." : "WhatsApp:MetaWebhookVerifyToken is missing; Meta delivery/read webhook verification will fail."),
            new(
                "AD_BANNERS",
                "Invoice ad banners",
                activeBannerCount > 0 ? "Passed" : "Warning",
                activeBannerCount > 0 ? $"{activeBannerCount} active banner(s) found." : "No active invoice marketing banners are configured."),
            new(
                "CAMPAIGNS",
                "Campaign workflow",
                campaignCount > 0 ? "Passed" : "Warning",
                campaignCount > 0 ? $"{campaignCount} campaign(s) found." : "No campaigns created yet. Create a test campaign from Campaign Audiences."),
            new(
                "FEEDBACK_CAPTURE",
                "Customer feedback capture",
                feedbackCount > 0 ? "Passed" : "Warning",
                feedbackCount > 0 ? $"{feedbackCount} feedback response(s) found." : "No customer feedback found yet. Submit a test feedback from a public invoice link."),
            new(
                "ACTIVE_LINKS",
                "Active public links",
                activeDigitalBillCount > 0 ? "Passed" : "Warning",
                activeDigitalBillCount > 0 ? $"{activeDigitalBillCount} active public invoice link(s) available." : "No active public invoice links are available.")
        };

        var failed = checks.Count(item => item.Status == "Failed");
        var warnings = checks.Count(item => item.Status == "Warning");
        var overall = failed > 0 ? "Failed" : warnings > 0 ? "Warning" : "Passed";
        return new DigitalBillProductionReadinessDto(
            overall,
            Now(),
            publicBaseUrl,
            !string.IsNullOrWhiteSpace(webhookToken),
            digitalBillCount,
            activeDigitalBillCount,
            whatsappSettingsCount,
            enabledWhatsAppSettingsCount,
            metaWhatsAppSettingsCount,
            reviewSettingsCount,
            activeBannerCount,
            campaignCount,
            feedbackCount,
            checks);
    }


    private static async Task<DigitalBillCampaignListResponseDto> ListDigitalBillCampaignsAsync(
        HttpContext context,
        GarmetixDbContext db,
        int page = 1,
        int pageSize = 50,
        string? status = null,
        string? q = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 200);
        var query = WorkspaceScope.ApplyTo(db.DigitalBillCampaigns.AsNoTracking(), context)
            .Where(item => !item.Deleted);

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            var statusTerm = status.Trim();
            query = query.Where(item => item.Status == statusTerm);
        }

        var term = q?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(item => item.Name.ToLower().Contains(term) || item.MessageTitle.ToLower().Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var rowsRaw = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .GroupJoin(db.Stores.AsNoTracking(), campaign => campaign.StoreId, store => (Guid?)store.Id, (campaign, stores) => new { campaign, store = stores.FirstOrDefault() })
            .ToListAsync(cancellationToken);
        var rows = rowsRaw
            .Select(item => ToCampaignDto(item.campaign, item.store?.Name))
            .ToList();

        return new DigitalBillCampaignListResponseDto(rows, total, page, pageSize);
    }

    private static async Task<IResult> GetDigitalBillCampaignAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var campaignRow = await WorkspaceScope.ApplyTo(db.DigitalBillCampaigns.AsNoTracking(), context)
            .Where(item => item.Id == id && !item.Deleted)
            .GroupJoin(db.Stores.AsNoTracking(), campaign => campaign.StoreId, store => (Guid?)store.Id, (campaign, stores) => new { campaign, store = stores.FirstOrDefault() })
            .FirstOrDefaultAsync(cancellationToken);
        if (campaignRow is null)
        {
            return Results.NotFound(new { message = "Campaign was not found in your workspace." });
        }

        var recipients = await CampaignRecipientQuery(context, db, id)
            .OrderBy(item => item.CustomerName)
            .ThenBy(item => item.CustomerMobile)
            .Take(1000)
            .ToListAsync(cancellationToken);

        return Results.Ok(new DigitalBillCampaignDetailDto(
            ToCampaignDto(campaignRow.campaign, campaignRow.store?.Name),
            recipients));
    }

    private static async Task<IResult> GetDigitalBillCampaignRoiAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        int days = 30,
        CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days <= 0 ? 30 : days, 1, 365);
        var campaign = await WorkspaceScope.ApplyTo(db.DigitalBillCampaigns.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (campaign is null)
        {
            return Results.NotFound(new { message = "Campaign was not found in your workspace." });
        }

        var recipients = await WorkspaceScope.ApplyTo(db.DigitalBillCampaignRecipients.AsNoTracking(), context)
            .Where(item => item.CampaignId == id && !item.Deleted)
            .ToListAsync(cancellationToken);

        var attributionFrom = campaign.CompletedAt ?? campaign.QueuedAt ?? recipients
            .Where(item => item.SentAt.HasValue)
            .Select(item => item.SentAt!.Value)
            .DefaultIfEmpty(campaign.CreatedAt)
            .Min();
        var attributionTo = attributionFrom.AddDays(days);
        if (attributionTo > Now())
        {
            attributionTo = Now();
        }

        var digitalInvoiceIds = recipients.Select(item => item.DigitalInvoiceId).Distinct().ToList();
        var sourceInvoiceIds = digitalInvoiceIds.Count == 0
            ? new List<Guid>()
            : await WorkspaceScope.ApplyTo(db.DigitalInvoices.AsNoTracking(), context)
                .Where(item => digitalInvoiceIds.Contains(item.Id) && !item.Deleted)
                .Select(item => item.InvoiceId)
                .Distinct()
                .ToListAsync(cancellationToken);

        var eventRows = digitalInvoiceIds.Count == 0
            ? new List<CampaignRoiEventRow>()
            : await WorkspaceScope.ApplyTo(db.DigitalInvoiceEvents.AsNoTracking(), context)
                .Where(item => digitalInvoiceIds.Contains(item.DigitalInvoiceId)
                    && !item.Deleted
                    && item.EventAt >= attributionFrom
                    && item.EventAt <= attributionTo)
                .Select(item => new CampaignRoiEventRow(item.DigitalInvoiceId, item.EventType, item.EventAt))
                .ToListAsync(cancellationToken);

        var recipientMobileSet = recipients
            .Select(item => NormalizeMobileDigits(item.CustomerMobile))
            .Where(item => item.Length >= 6)
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        var saleRowsRaw = recipientMobileSet.Count == 0
            ? new List<CampaignRoiSaleRow>()
            : await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
                .Where(item => !item.Deleted
                    && !item.ReturnInvoice
                    && item.OnDate >= attributionFrom.Date
                    && item.OnDate <= attributionTo
                    && !sourceInvoiceIds.Contains(item.Id))
                .GroupJoin(db.Stores.AsNoTracking(), invoice => invoice.StoreId, store => store.Id, (invoice, stores) => new { invoice, store = stores.FirstOrDefault() })
                .Select(item => new CampaignRoiSaleRow(
                    item.invoice.Id,
                    item.invoice.InvoiceNumber,
                    item.invoice.OnDate,
                    item.invoice.CustomerName ?? string.Empty,
                    item.invoice.CustomerMobileNumber,
                    item.invoice.StoreId,
                    item.store != null ? item.store.Name : string.Empty,
                    item.invoice.BillAmount))
                .ToListAsync(cancellationToken);

        var repeatSales = saleRowsRaw
            .Where(item => recipientMobileSet.Contains(NormalizeMobileDigits(item.CustomerMobile)))
            .OrderByDescending(item => item.InvoiceDate)
            .ToList();
        var eventLookup = eventRows.GroupBy(item => item.DigitalInvoiceId).ToDictionary(group => group.Key, group => group.ToList());
        var saleLookup = repeatSales
            .GroupBy(item => NormalizeMobileDigits(item.CustomerMobile))
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        var recipientRoi = recipients
            .OrderBy(item => item.CustomerName)
            .ThenBy(item => item.CustomerMobile)
            .Select(recipient =>
            {
                var events = eventLookup.TryGetValue(recipient.DigitalInvoiceId, out var rows) ? rows : new List<CampaignRoiEventRow>();
                var mobile = NormalizeMobileDigits(recipient.CustomerMobile);
                var sales = saleLookup.TryGetValue(mobile, out var saleRows) ? saleRows : new List<CampaignRoiSaleRow>();
                return new DigitalBillCampaignRecipientRoiDto(
                    recipient.Id,
                    recipient.DigitalInvoiceId,
                    recipient.CustomerName,
                    recipient.CustomerMobile,
                    recipient.InvoiceNumber,
                    recipient.PublicPath,
                    recipient.Status,
                    recipient.SentAt,
                    events.Count(item => item.EventType == "Opened"),
                    events.Count(item => item.EventType == "PdfDownloaded"),
                    events.Count(item => item.EventType == "ReviewClicked"),
                    events.Count(item => item.EventType == "FeedbackSubmitted"),
                    events.Count(item => item.EventType == "BannerClicked"),
                    sales.Count,
                    sales.Sum(item => item.BillAmount),
                    sales.Count == 0 ? null : sales.Max(item => (DateTime?)item.InvoiceDate));
            })
            .ToList();

        var openedAfter = eventRows.Count(item => item.EventType == "Opened");
        var pdfAfter = eventRows.Count(item => item.EventType == "PdfDownloaded");
        var reviewAfter = eventRows.Count(item => item.EventType == "ReviewClicked");
        var feedbackAfter = eventRows.Count(item => item.EventType == "FeedbackSubmitted");
        var bannerAfter = eventRows.Count(item => item.EventType == "BannerClicked");
        var engagedRecipientCount = recipientRoi.Count(item => item.OpenedAfterCount > 0
            || item.PdfDownloadedAfterCount > 0
            || item.ReviewClickedAfterCount > 0
            || item.FeedbackSubmittedAfterCount > 0
            || item.BannerClickedAfterCount > 0);
        var repeatCustomerCount = repeatSales.Select(item => NormalizeMobileDigits(item.CustomerMobile)).Where(item => item.Length >= 6).Distinct().Count();
        var repeatSalesAmount = repeatSales.Sum(item => item.BillAmount);
        var recipientCount = Math.Max(0, recipients.Count);

        var dto = new DigitalBillCampaignRoiDto(
            campaign.Id,
            campaign.Name,
            campaign.Status,
            attributionFrom,
            attributionTo,
            days,
            recipientCount,
            recipients.Count(item => item.Status.Equals("Sent", StringComparison.OrdinalIgnoreCase) || item.SentAt.HasValue),
            openedAfter,
            pdfAfter,
            reviewAfter,
            feedbackAfter,
            bannerAfter,
            engagedRecipientCount,
            repeatCustomerCount,
            repeatSales.Count,
            repeatSalesAmount,
            repeatSales.Count == 0 ? 0 : repeatSalesAmount / repeatSales.Count,
            recipientCount == 0 ? 0 : Math.Round((decimal)engagedRecipientCount * 100m / recipientCount, 2),
            recipientCount == 0 ? 0 : Math.Round((decimal)repeatCustomerCount * 100m / recipientCount, 2),
            recipientRoi.Take(1000).ToList(),
            repeatSales.Take(500).Select(item => new DigitalBillCampaignRepeatSaleDto(
                item.InvoiceId,
                item.InvoiceNumber,
                item.InvoiceDate,
                string.IsNullOrWhiteSpace(item.CustomerName) ? "Walk-in Customer" : item.CustomerName,
                item.CustomerMobile,
                item.StoreId,
                string.IsNullOrWhiteSpace(item.StoreName) ? "Store" : item.StoreName,
                item.BillAmount)).ToList());

        return Results.Ok(dto);
    }

    private static async Task<IResult> PreviewDigitalBillCampaignAsync(
        DigitalBillCampaignPreviewRequest request,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.Limit <= 0 ? 1000 : request.Limit, 1, 5000);
        var segment = NormalizeAudienceSegment(request.Segment);
        var to = (request.ToDate ?? Now()).Date.AddDays(1).AddTicks(-1);
        var from = (request.FromDate ?? to.Date.AddDays(-29)).Date;
        var candidates = await BuildCampaignAudienceAsync(context, db, from, to, request.StoreId, segment, request.SearchText, limit, cancellationToken);
        var withMobile = candidates.Where(item => !string.IsNullOrWhiteSpace(item.CustomerMobile)).Take(limit).ToList();
        var sample = withMobile.Take(25)
            .Select(item => ToPreviewRecipient(item, Guid.Empty, RenderCampaignMessage(request.MessageBody, request.MessageTitle, request.OfferUrl, item)))
            .ToList();

        return Results.Ok(new DigitalBillCampaignPreviewDto(
            segment,
            from,
            to,
            request.StoreId,
            request.SearchText,
            candidates.Count,
            withMobile.Count,
            candidates.Count(item => string.IsNullOrWhiteSpace(item.CustomerMobile)),
            limit,
            sample));
    }

    private static async Task<IResult> CreateDigitalBillCampaignAsync(
        DigitalBillCampaignCreateRequest request,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.Limit <= 0 ? 1000 : request.Limit, 1, 5000);
        var segment = NormalizeAudienceSegment(request.Segment);
        var to = (request.ToDate ?? Now()).Date.AddDays(1).AddTicks(-1);
        var from = (request.FromDate ?? to.Date.AddDays(-29)).Date;
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(new { message = "Campaign name is required." });
        }
        if (string.IsNullOrWhiteSpace(request.MessageBody))
        {
            return Results.BadRequest(new { message = "Campaign message body is required." });
        }

        var scope = await ResolveCampaignScopeAsync(request.StoreId, context, db, cancellationToken);
        if (!string.IsNullOrWhiteSpace(scope.Error))
        {
            return Results.BadRequest(new { message = scope.Error });
        }

        var candidates = await BuildCampaignAudienceAsync(context, db, from, to, request.StoreId, segment, request.SearchText, limit, cancellationToken);
        var recipientCandidates = candidates
            .Where(item => !string.IsNullOrWhiteSpace(item.CustomerMobile))
            .Take(limit)
            .ToList();
        if (recipientCandidates.Count == 0)
        {
            return Results.BadRequest(new { message = "No campaign recipients with mobile numbers were found for this segment/filter." });
        }

        var now = Now();
        var campaign = new DigitalBillCampaign
        {
            Id = Guid.NewGuid(),
            CompanyId = scope.CompanyId,
            StoreGroupId = scope.StoreGroupId,
            StoreId = scope.StoreId,
            Name = request.Name.Trim(),
            Segment = segment,
            FromDate = from,
            ToDate = to,
            SearchText = string.IsNullOrWhiteSpace(request.SearchText) ? null : request.SearchText.Trim(),
            Channel = NormalizeCampaignChannel(request.Channel),
            Status = request.QueueNow ? "Queued" : "Draft",
            TemplateName = string.IsNullOrWhiteSpace(request.TemplateName) ? null : request.TemplateName.Trim(),
            MessageTitle = string.IsNullOrWhiteSpace(request.MessageTitle) ? "Digital bill campaign" : request.MessageTitle.Trim(),
            MessageBody = request.MessageBody.Trim(),
            OfferUrl = CleanUrl(request.OfferUrl),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            RecipientCount = recipientCandidates.Count,
            PreparedCount = request.QueueNow ? 0 : recipientCandidates.Count,
            SentCount = 0,
            FailedCount = 0,
            OpenCountAtCreate = recipientCandidates.Sum(item => item.OpenCount),
            ReviewClickCountAtCreate = recipientCandidates.Sum(item => item.ReviewClickCount),
            FeedbackCountAtCreate = recipientCandidates.Sum(item => item.FeedbackCount),
            ScheduledAt = request.ScheduledAt,
            QueuedAt = request.QueueNow ? now : null,
            CreatedBy = ResolveActor(context),
            CreatedAt = now,
            UpdatedAt = now
        };
        db.DigitalBillCampaigns.Add(campaign);

        foreach (var item in recipientCandidates)
        {
            var message = RenderCampaignMessage(campaign.MessageBody, campaign.MessageTitle, campaign.OfferUrl, item);
            db.DigitalBillCampaignRecipients.Add(new DigitalBillCampaignRecipient
            {
                Id = Guid.NewGuid(),
                CampaignId = campaign.Id,
                DigitalInvoiceId = item.LastDigitalBillId,
                AudienceKey = item.AudienceKey,
                CustomerName = item.CustomerName,
                CustomerMobile = item.CustomerMobile,
                InvoiceNumber = item.LastInvoiceNumber,
                PublicPath = item.LastPublicPath,
                LastInvoiceAmount = item.TotalAmount,
                Status = request.QueueNow ? "ManualPending" : "Prepared",
                MessageBody = message,
                QueuedAt = request.QueueNow ? now : null,
                CompanyId = scope.CompanyId,
                StoreGroupId = item.StoreId.HasValue ? await ResolveStoreGroupForStoreAsync(item.StoreId.Value, db, cancellationToken) ?? scope.StoreGroupId ?? Guid.Empty : scope.StoreGroupId ?? Guid.Empty,
                StoreId = item.StoreId ?? scope.StoreId ?? Guid.Empty,
                CreatedBy = ResolveActor(context),
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return await GetDigitalBillCampaignAsync(campaign.Id, context, db, cancellationToken);
    }



    private static async Task<IResult> SendDigitalBillCampaignWhatsAppAsync(
        Guid id,
        DigitalBillCampaignSendRequest request,
        DigitalBillWhatsAppService whatsApp,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await whatsApp.SendMarketingCampaignAsync(id, request, context, cancellationToken);
        return result is null
            ? Results.NotFound(new { message = "Campaign was not found in your workspace." })
            : Results.Ok(result);
    }


    private static async Task<IResult> QueueDigitalBillCampaignAsync(
        Guid id,
        DigitalBillCampaignStatusRequest request,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var campaign = await WorkspaceScope.ApplyTo(db.DigitalBillCampaigns, context)
            .FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (campaign is null)
        {
            return Results.NotFound(new { message = "Campaign was not found in your workspace." });
        }

        var now = Now();
        campaign.Status = "Queued";
        campaign.QueuedAt ??= now;
        campaign.PreparedCount = 0;
        campaign.UpdatedAt = now;
        var recipients = await WorkspaceScope.ApplyTo(db.DigitalBillCampaignRecipients, context)
            .Where(item => item.CampaignId == id && !item.Deleted && (item.Status == "Prepared" || item.Status == "Draft"))
            .ToListAsync(cancellationToken);
        foreach (var item in recipients)
        {
            item.Status = "ManualPending";
            item.QueuedAt ??= now;
            item.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return await GetDigitalBillCampaignAsync(id, context, db, cancellationToken);
    }

    private static async Task<IResult> MarkDigitalBillCampaignSentAsync(
        Guid id,
        DigitalBillCampaignStatusRequest request,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var campaign = await WorkspaceScope.ApplyTo(db.DigitalBillCampaigns, context)
            .FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (campaign is null)
        {
            return Results.NotFound(new { message = "Campaign was not found in your workspace." });
        }

        var now = Now();
        var recipients = await WorkspaceScope.ApplyTo(db.DigitalBillCampaignRecipients, context)
            .Where(item => item.CampaignId == id && !item.Deleted && item.Status != "Sent")
            .ToListAsync(cancellationToken);
        foreach (var item in recipients)
        {
            item.Status = "Sent";
            item.SentAt ??= now;
            item.UpdatedAt = now;
        }

        campaign.Status = "Completed";
        campaign.CompletedAt ??= now;
        campaign.SentCount = await WorkspaceScope.ApplyTo(db.DigitalBillCampaignRecipients.AsNoTracking(), context)
            .CountAsync(item => item.CampaignId == id && !item.Deleted && item.Status == "Sent", cancellationToken) + recipients.Count(item => item.Status == "Sent");
        campaign.PreparedCount = 0;
        campaign.FailedCount = await WorkspaceScope.ApplyTo(db.DigitalBillCampaignRecipients.AsNoTracking(), context)
            .CountAsync(item => item.CampaignId == id && !item.Deleted && item.Status == "Failed", cancellationToken);
        campaign.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return await GetDigitalBillCampaignAsync(id, context, db, cancellationToken);
    }

    private static async Task<IResult> CancelDigitalBillCampaignAsync(
        Guid id,
        DigitalBillCampaignStatusRequest request,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var campaign = await WorkspaceScope.ApplyTo(db.DigitalBillCampaigns, context)
            .FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (campaign is null)
        {
            return Results.NotFound(new { message = "Campaign was not found in your workspace." });
        }
        if (campaign.Status == "Completed")
        {
            return Results.BadRequest(new { message = "Completed campaigns cannot be cancelled." });
        }

        var now = Now();
        campaign.Status = "Cancelled";
        campaign.CancelledAt ??= now;
        campaign.Notes = string.IsNullOrWhiteSpace(request.Reason) ? campaign.Notes : $"{campaign.Notes}\nCancelled: {request.Reason}".Trim();
        campaign.UpdatedAt = now;
        var recipients = await WorkspaceScope.ApplyTo(db.DigitalBillCampaignRecipients, context)
            .Where(item => item.CampaignId == id && !item.Deleted && item.Status != "Sent")
            .ToListAsync(cancellationToken);
        foreach (var item in recipients)
        {
            item.Status = "Cancelled";
            item.UpdatedAt = now;
        }
        await db.SaveChangesAsync(cancellationToken);
        return await GetDigitalBillCampaignAsync(id, context, db, cancellationToken);
    }

    private static async Task<List<DigitalBillAudienceItemDto>> BuildCampaignAudienceAsync(
        HttpContext context,
        GarmetixDbContext db,
        DateTime from,
        DateTime to,
        Guid? storeId,
        string segment,
        string? q,
        int limit,
        CancellationToken cancellationToken)
    {
        const int maxRows = 10000;
        var billQuery = WorkspaceScope.ApplyTo(db.DigitalInvoices.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.InvoiceDate >= from && item.InvoiceDate <= to);
        if (storeId.HasValue)
        {
            billQuery = billQuery.Where(item => item.StoreId == storeId.Value);
        }
        var term = q?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(term))
        {
            billQuery = billQuery.Where(item => item.InvoiceNumber.ToLower().Contains(term) || item.CustomerName.ToLower().Contains(term) || item.CustomerMobile.ToLower().Contains(term));
        }

        var invoiceRows = await (
            from bill in billQuery
            join store in db.Stores.AsNoTracking() on bill.StoreId equals store.Id into stores
            from store in stores.DefaultIfEmpty()
            orderby bill.InvoiceDate descending, bill.CreatedAt descending
            select new CampaignInvoiceRow(
                bill.Id,
                bill.StoreId,
                store != null ? store.Name : string.Empty,
                bill.InvoiceNumber,
                bill.InvoiceDate,
                bill.CustomerName,
                bill.CustomerMobile,
                bill.Amount,
                bill.PublicToken,
                bill.OpenCount,
                bill.PdfDownloadCount,
                bill.ReviewClickCount,
                bill.FeedbackCount,
                bill.WhatsAppStatus,
                bill.CreatedAt,
                bill.LastOpenedAt,
                bill.LastWhatsAppSentAt))
            .Take(maxRows)
            .ToListAsync(cancellationToken);

        var invoiceIds = invoiceRows.Select(item => item.Id).ToList();
        var eventRows = invoiceIds.Count == 0 ? new List<AudienceEventRow>() : await db.DigitalInvoiceEvents.AsNoTracking()
            .Where(item => invoiceIds.Contains(item.DigitalInvoiceId) && !item.Deleted)
            .Select(item => new AudienceEventRow(item.DigitalInvoiceId, item.EventType, item.EventAt))
            .ToListAsync(cancellationToken);
        var feedbackRows = invoiceIds.Count == 0 ? new List<AudienceFeedbackRow>() : await db.CustomerFeedback.AsNoTracking()
            .Where(item => invoiceIds.Contains(item.DigitalInvoiceId) && !item.Deleted)
            .Select(item => new AudienceFeedbackRow(item.DigitalInvoiceId, item.Rating, item.SubmittedAt))
            .ToListAsync(cancellationToken);
        var whatsappRows = invoiceIds.Count == 0 ? new List<AudienceWhatsAppRow>() : await db.WhatsAppMessageLogs.AsNoTracking()
            .Where(item => item.DigitalInvoiceId.HasValue && invoiceIds.Contains(item.DigitalInvoiceId.Value) && !item.Deleted)
            .Select(item => new AudienceWhatsAppRow(item.DigitalInvoiceId.Value, item.Status, item.CreatedAt, item.SentAt, item.DeliveredAt, item.ReadAt))
            .ToListAsync(cancellationToken);

        var eventLookup = eventRows.GroupBy(item => item.DigitalInvoiceId).ToDictionary(group => group.Key, group => group.ToList());
        var feedbackLookup = feedbackRows.GroupBy(item => item.DigitalInvoiceId).ToDictionary(group => group.Key, group => group.ToList());
        var whatsappLookup = whatsappRows.GroupBy(item => item.DigitalInvoiceId).ToDictionary(group => group.Key, group => group.ToList());

        var audience = invoiceRows
            .GroupBy(item => BuildAudienceKey(item.CustomerMobile, item.CustomerName, item.Id))
            .Select(group =>
            {
                var invoices = group.OrderByDescending(item => item.InvoiceDate).ThenByDescending(item => item.CreatedAt).ToList();
                var last = invoices[0];
                var groupEvents = invoices.SelectMany(item => eventLookup.TryGetValue(item.Id, out var rows) ? rows : Enumerable.Empty<AudienceEventRow>()).ToList();
                var groupFeedback = invoices.SelectMany(item => feedbackLookup.TryGetValue(item.Id, out var rows) ? rows : Enumerable.Empty<AudienceFeedbackRow>()).ToList();
                var groupWhatsApp = invoices.SelectMany(item => whatsappLookup.TryGetValue(item.Id, out var rows) ? rows : Enumerable.Empty<AudienceWhatsAppRow>()).ToList();
                var openCount = invoices.Sum(item => item.OpenCount);
                var pdfCount = invoices.Sum(item => item.PdfDownloadCount);
                var reviewCount = invoices.Sum(item => item.ReviewClickCount);
                var feedbackCount = Math.Max(invoices.Sum(item => item.FeedbackCount), groupFeedback.Count);
                var bannerClickCount = groupEvents.Count(item => item.EventType == "BannerClicked");
                var sentCount = groupWhatsApp.Count(item => IsWhatsAppSuccessful(item.Status));
                var failedCount = groupWhatsApp.Count(item => IsWhatsAppFailed(item.Status)) + invoices.Count(item => IsWhatsAppFailed(item.WhatsAppStatus));
                var lastFeedback = groupFeedback.OrderByDescending(item => item.SubmittedAt).FirstOrDefault();
                var worstFeedback = groupFeedback.Count == 0 ? null : groupFeedback.Min(item => (int?)item.Rating);
                var lastActivityCandidates = new List<DateTime?>
                {
                    last.LastOpenedAt,
                    last.LastWhatsAppSentAt,
                    last.CreatedAt,
                    groupEvents.Count == 0 ? null : groupEvents.Max(item => (DateTime?)item.EventAt),
                    groupFeedback.Count == 0 ? null : groupFeedback.Max(item => (DateTime?)item.SubmittedAt),
                    groupWhatsApp.Count == 0 ? null : groupWhatsApp.Max(item => (DateTime?)LatestWhatsAppActivityAt(item.CreatedAt, item.SentAt, item.DeliveredAt, item.ReadAt))
                };
                var lastActivityAt = lastActivityCandidates.Where(item => item.HasValue).Max();
                return new DigitalBillAudienceItemDto(
                    group.Key,
                    string.IsNullOrWhiteSpace(last.CustomerName) ? "Walk-in Customer" : last.CustomerName,
                    last.CustomerMobile,
                    last.StoreId,
                    string.IsNullOrWhiteSpace(last.StoreName) ? "Store" : last.StoreName,
                    last.Id,
                    last.InvoiceNumber,
                    last.InvoiceDate,
                    $"/i/{Uri.EscapeDataString(last.PublicToken)}",
                    invoices.Count,
                    invoices.Sum(item => item.Amount),
                    openCount,
                    pdfCount,
                    reviewCount,
                    feedbackCount,
                    bannerClickCount,
                    sentCount,
                    failedCount,
                    last.WhatsAppStatus,
                    lastFeedback?.Rating,
                    worstFeedback,
                    lastActivityAt,
                    BuildAudienceRecommendation(last.CustomerMobile, openCount, reviewCount, feedbackCount, worstFeedback, failedCount, last.WhatsAppStatus, sentCount));
            })
            .Where(item => MatchesAudienceSegment(item, segment))
            .OrderByDescending(item => item.LastActivityAt ?? item.LastInvoiceDate)
            .Take(limit)
            .ToList();

        return audience;
    }

    private static IQueryable<DigitalBillCampaignRecipientDto> CampaignRecipientQuery(HttpContext context, GarmetixDbContext db, Guid campaignId)
        => WorkspaceScope.ApplyTo(db.DigitalBillCampaignRecipients.AsNoTracking(), context)
            .Where(item => item.CampaignId == campaignId && !item.Deleted)
            .GroupJoin(db.Stores.AsNoTracking(), recipient => recipient.StoreId, store => store.Id, (recipient, stores) => new { recipient, store = stores.FirstOrDefault() })
            .Select(item => new DigitalBillCampaignRecipientDto(
                item.recipient.Id,
                item.recipient.CampaignId,
                item.recipient.DigitalInvoiceId,
                item.recipient.AudienceKey,
                item.recipient.CustomerName,
                item.recipient.CustomerMobile,
                item.recipient.StoreId,
                item.store != null ? item.store.Name : string.Empty,
                item.recipient.InvoiceNumber,
                item.recipient.PublicPath,
                item.recipient.LastInvoiceAmount,
                item.recipient.Status,
                item.recipient.MessageBody,
                item.recipient.ErrorMessage,
                item.recipient.QueuedAt,
                item.recipient.SentAt,
                item.recipient.FailedAt,
                item.recipient.CreatedAt));

    private static DigitalBillCampaignDto ToCampaignDto(DigitalBillCampaign campaign, string? storeName)
        => new(
            campaign.Id,
            campaign.CompanyId,
            campaign.StoreGroupId,
            campaign.StoreId,
            storeName,
            campaign.Name,
            campaign.Segment,
            campaign.FromDate,
            campaign.ToDate,
            campaign.SearchText,
            campaign.Channel,
            campaign.Status,
            campaign.TemplateName,
            campaign.MessageTitle,
            campaign.MessageBody,
            campaign.OfferUrl,
            campaign.Notes,
            campaign.RecipientCount,
            campaign.PreparedCount,
            campaign.SentCount,
            campaign.FailedCount,
            campaign.OpenCountAtCreate,
            campaign.ReviewClickCountAtCreate,
            campaign.FeedbackCountAtCreate,
            campaign.ScheduledAt,
            campaign.QueuedAt,
            campaign.CompletedAt,
            campaign.CancelledAt,
            campaign.CreatedAt,
            campaign.UpdatedAt);

    private static DigitalBillCampaignRecipientDto ToPreviewRecipient(DigitalBillAudienceItemDto item, Guid campaignId, string message)
        => new(
            Guid.Empty,
            campaignId,
            item.LastDigitalBillId,
            item.AudienceKey,
            item.CustomerName,
            item.CustomerMobile,
            item.StoreId ?? Guid.Empty,
            item.StoreName,
            item.LastInvoiceNumber,
            item.LastPublicPath,
            item.TotalAmount,
            "Preview",
            message,
            null,
            null,
            null,
            null,
            Now());

    private static async Task<(Guid CompanyId, Guid? StoreGroupId, Guid? StoreId, string? Error)> ResolveCampaignScopeAsync(Guid? storeId, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (storeId.HasValue)
        {
            var store = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
                .Where(item => item.Id == storeId.Value && !item.Deleted)
                .Select(item => new { item.CompanyId, item.StoreGroupId, item.Id })
                .FirstOrDefaultAsync(cancellationToken);
            return store is null
                ? (Guid.Empty, null, null, "Selected store is outside your workspace.")
                : (store.CompanyId, store.StoreGroupId, store.Id, null);
        }

        var companyId = WorkspaceScope.ClaimGuid(context, "companyId");
        var storeGroupId = WorkspaceScope.ClaimGuid(context, "storeGroupId");
        var scopedStoreId = WorkspaceScope.ClaimGuid(context, "storeId");
        if (scopedStoreId.HasValue)
        {
            var store = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
                .Where(item => item.Id == scopedStoreId.Value && !item.Deleted)
                .Select(item => new { item.CompanyId, item.StoreGroupId, item.Id })
                .FirstOrDefaultAsync(cancellationToken);
            return store is null
                ? (Guid.Empty, null, null, "Your assigned store could not be resolved.")
                : (store.CompanyId, store.StoreGroupId, store.Id, null);
        }

        if (companyId.HasValue && companyId.Value != Guid.Empty)
        {
            return (companyId.Value, storeGroupId, null, null);
        }

        var fallback = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .Where(item => !item.Deleted)
            .Select(item => new { item.CompanyId, item.StoreGroupId, item.Id })
            .FirstOrDefaultAsync(cancellationToken);
        return fallback is null
            ? (Guid.Empty, null, null, "Company or store is required for campaign creation.")
            : (fallback.CompanyId, fallback.StoreGroupId, null, null);
    }

    private static async Task<Guid?> ResolveStoreGroupForStoreAsync(Guid storeId, GarmetixDbContext db, CancellationToken cancellationToken)
        => await db.Stores.AsNoTracking()
            .Where(item => item.Id == storeId && !item.Deleted)
            .Select(item => (Guid?)item.StoreGroupId)
            .FirstOrDefaultAsync(cancellationToken);

    private static string NormalizeCampaignChannel(string? value)
    {
        var normalized = Regex.Replace(value ?? string.Empty, @"[^A-Za-z]", string.Empty);
        return normalized.Equals("SmsManual", StringComparison.OrdinalIgnoreCase) ? "SmsManual"
            : normalized.Equals("WhatsAppProvider", StringComparison.OrdinalIgnoreCase) ? "WhatsAppProvider"
            : normalized.Equals("WhatsAppMarketingTemplate", StringComparison.OrdinalIgnoreCase) ? "WhatsAppMarketingTemplate"
            : "WhatsAppManual";
    }

    private static string RenderCampaignMessage(string? body, string? title, string? offerUrl, DigitalBillAudienceItemDto item)
    {
        var template = string.IsNullOrWhiteSpace(body)
            ? "Hello {{customerName}}, thank you for shopping with us. Your latest bill {{invoiceNumber}} is available here: {{publicUrl}}"
            : body.Trim();
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["customerName"] = string.IsNullOrWhiteSpace(item.CustomerName) ? "Customer" : item.CustomerName,
            ["mobile"] = item.CustomerMobile,
            ["storeName"] = item.StoreName,
            ["invoiceNumber"] = item.LastInvoiceNumber,
            ["publicUrl"] = item.LastPublicPath,
            ["billLink"] = item.LastPublicPath,
            ["amount"] = item.TotalAmount.ToString("0.##"),
            ["offerUrl"] = offerUrl ?? string.Empty,
            ["campaignTitle"] = title ?? "Digital bill campaign"
        };
        foreach (var pair in replacements)
        {
            template = template.Replace("{{" + pair.Key + "}}", pair.Value, StringComparison.OrdinalIgnoreCase);
        }
        return template;
    }

    private sealed record CampaignRoiEventRow(Guid DigitalInvoiceId, string EventType, DateTime EventAt);

    private sealed record CampaignRoiSaleRow(
        Guid InvoiceId,
        string InvoiceNumber,
        DateTime InvoiceDate,
        string CustomerName,
        string CustomerMobile,
        Guid StoreId,
        string StoreName,
        decimal BillAmount);

    private sealed record CampaignInvoiceRow(
        Guid Id,
        Guid StoreId,
        string StoreName,
        string InvoiceNumber,
        DateTime InvoiceDate,
        string CustomerName,
        string CustomerMobile,
        decimal Amount,
        string PublicToken,
        int OpenCount,
        int PdfDownloadCount,
        int ReviewClickCount,
        int FeedbackCount,
        string WhatsAppStatus,
        DateTime CreatedAt,
        DateTime? LastOpenedAt,
        DateTime? LastWhatsAppSentAt);


    private sealed record AudienceEventRow(Guid DigitalInvoiceId, string EventType, DateTime EventAt);

    private sealed record AudienceFeedbackRow(Guid DigitalInvoiceId, int Rating, DateTime SubmittedAt);

    private sealed record AudienceWhatsAppRow(Guid DigitalInvoiceId, string Status, DateTime CreatedAt, DateTime? SentAt, DateTime? DeliveredAt, DateTime? ReadAt);

    private static string NormalizeAudienceSegment(string? value)
    {
        var normalized = Regex.Replace(value ?? string.Empty, @"[^A-Za-z]", string.Empty);
        return normalized.ToLowerInvariant() switch
        {
            "opened" => "opened",
            "notopened" => "notOpened",
            "pdfdownloaded" => "pdfDownloaded",
            "reviewclicked" => "reviewClicked",
            "reviewpending" => "reviewPending",
            "feedbacksubmitted" => "feedbackSubmitted",
            "lowfeedback" => "lowFeedback",
            "whatsappfailed" => "whatsappFailed",
            "whatsapppending" => "whatsappPending",
            "nomobile" => "noMobile",
            _ => "all"
        };
    }

    private static string NormalizeMobileDigits(string? mobile)
        => new string((mobile ?? string.Empty).Where(char.IsDigit).ToArray());

    private static string BuildAudienceKey(string? mobile, string? customerName, Guid fallbackId)
    {
        var digits = new string((mobile ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length >= 6)
        {
            return "mobile:" + digits;
        }

        var name = Regex.Replace(customerName ?? string.Empty, @"\s+", " ").Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(name) ? "invoice:" + fallbackId.ToString("N") : "name:" + name;
    }

    private static IReadOnlyList<DigitalBillAudienceSummaryDto> BuildAudienceSummaries(IReadOnlyList<DigitalBillAudienceItemDto> audience)
        => new[]
        {
            new DigitalBillAudienceSummaryDto("all", "All customers", audience.Count, "Every customer with a digital bill in the period."),
            new DigitalBillAudienceSummaryDto("opened", "Opened bill", audience.Count(item => item.OpenCount > 0), "Customers who opened the hosted invoice."),
            new DigitalBillAudienceSummaryDto("notOpened", "Not opened", audience.Count(item => item.OpenCount == 0), "Send a reminder or confirm mobile number."),
            new DigitalBillAudienceSummaryDto("pdfDownloaded", "PDF downloaded", audience.Count(item => item.PdfDownloadCount > 0), "Customers who downloaded the invoice PDF."),
            new DigitalBillAudienceSummaryDto("reviewClicked", "Review clicked", audience.Count(item => item.ReviewClickCount > 0), "Customers who clicked Google review."),
            new DigitalBillAudienceSummaryDto("reviewPending", "Review pending", audience.Count(item => item.OpenCount > 0 && item.ReviewClickCount == 0), "Opened invoice but did not click review."),
            new DigitalBillAudienceSummaryDto("feedbackSubmitted", "Feedback submitted", audience.Count(item => item.FeedbackCount > 0), "Customers who submitted private feedback."),
            new DigitalBillAudienceSummaryDto("lowFeedback", "Low feedback", audience.Count(item => item.WorstFeedbackRating.HasValue && item.WorstFeedbackRating <= 3), "Call and resolve before marketing."),
            new DigitalBillAudienceSummaryDto("whatsappFailed", "WhatsApp failed", audience.Count(item => item.WhatsAppFailedCount > 0), "Retry or fix provider/mobile issues."),
            new DigitalBillAudienceSummaryDto("whatsappPending", "WhatsApp pending", audience.Count(item => IsWhatsAppPending(item.LastWhatsAppStatus) || (item.WhatsAppSentCount == 0 && item.OpenCount == 0)), "Message not sent/read yet."),
            new DigitalBillAudienceSummaryDto("noMobile", "No mobile", audience.Count(item => string.IsNullOrWhiteSpace(item.CustomerMobile)), "Needs customer mobile before campaign.")
        };

    private static bool MatchesAudienceSegment(DigitalBillAudienceItemDto item, string segment)
        => segment switch
        {
            "opened" => item.OpenCount > 0,
            "notOpened" => item.OpenCount == 0,
            "pdfDownloaded" => item.PdfDownloadCount > 0,
            "reviewClicked" => item.ReviewClickCount > 0,
            "reviewPending" => item.OpenCount > 0 && item.ReviewClickCount == 0,
            "feedbackSubmitted" => item.FeedbackCount > 0,
            "lowFeedback" => item.WorstFeedbackRating.HasValue && item.WorstFeedbackRating <= 3,
            "whatsappFailed" => item.WhatsAppFailedCount > 0,
            "whatsappPending" => IsWhatsAppPending(item.LastWhatsAppStatus) || (item.WhatsAppSentCount == 0 && item.OpenCount == 0),
            "noMobile" => string.IsNullOrWhiteSpace(item.CustomerMobile),
            _ => true
        };

    private static bool IsWhatsAppSuccessful(string? status)
        => status is not null && (status.Equals("Sent", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Delivered", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Read", StringComparison.OrdinalIgnoreCase));

    private static bool IsWhatsAppFailed(string? status)
        => status is not null && (status.Equals("Failed", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Skipped", StringComparison.OrdinalIgnoreCase)
            || status.Equals("RetryLimitReached", StringComparison.OrdinalIgnoreCase));

    private static bool IsWhatsAppPending(string? status)
        => string.IsNullOrWhiteSpace(status)
            || status.Equals("NotSent", StringComparison.OrdinalIgnoreCase)
            || status.Equals("NotConfigured", StringComparison.OrdinalIgnoreCase)
            || status.Equals("ManualPending", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Queued", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Disabled", StringComparison.OrdinalIgnoreCase);

    private static DateTime LatestWhatsAppActivityAt(DateTime createdAt, DateTime? sentAt, DateTime? deliveredAt, DateTime? readAt)
        => new[] { (DateTime?)createdAt, sentAt, deliveredAt, readAt }
            .Where(item => item.HasValue)
            .Max()!.Value;

    private static string BuildAudienceRecommendation(
        string? mobile,
        int openCount,
        int reviewCount,
        int feedbackCount,
        int? worstFeedback,
        int whatsAppFailedCount,
        string? lastWhatsAppStatus,
        int whatsAppSentCount)
    {
        if (string.IsNullOrWhiteSpace(mobile))
        {
            return "Add mobile number before WhatsApp campaign.";
        }

        if (whatsAppFailedCount > 0 || IsWhatsAppFailed(lastWhatsAppStatus))
        {
            return "Fix WhatsApp delivery, then resend invoice link.";
        }

        if (worstFeedback.HasValue && worstFeedback <= 3)
        {
            return "Call customer first; do not push review request yet.";
        }

        if (openCount == 0)
        {
            return whatsAppSentCount > 0 ? "Send polite reminder to open bill." : "Send digital bill link on WhatsApp.";
        }

        if (reviewCount == 0)
        {
            return "Send honest Google review reminder.";
        }

        if (feedbackCount > 0)
        {
            return "Review feedback and tag for retention campaign.";
        }

        return "Ready for regular retention campaign.";
    }

    private static string? CleanUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"https://{trimmed}";
    }

    private static string ResolveActor(HttpContext context)
        => context.User.Identity?.Name
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
            ?? context.User.FindFirst("userName")?.Value
            ?? "System";

    private static string MaskMobile(string? mobile)
    {
        var digits = new string((mobile ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length <= 4)
        {
            return digits;
        }

        return new string('x', Math.Max(0, digits.Length - 4)) + digits[^4..];
    }

    private static DateTime Now() => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
}

using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Garmetix.Api.Billing;
using Garmetix.Api.Workspace;
using Garmetix.Core.Models.Inventory;
using Garmetix.Core.Models.Marketing;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Marketing;

public sealed class DigitalBillCrmService(GarmetixDbContext db, IConfiguration configuration)
{
    private const string SaleInvoiceType = "Sale";

    public async Task<DigitalBillActionResponseDto?> EnsureForSaleInvoiceAsync(Guid invoiceId, HttpContext context, CancellationToken cancellationToken)
    {
        var invoice = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == invoiceId && !item.Deleted, cancellationToken);
        if (invoice is null)
        {
            return null;
        }

        var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(item => item.Id == invoice.StoreId, cancellationToken);
        if (store is null)
        {
            return null;
        }

        var existing = await db.DigitalInvoices
            .FirstOrDefaultAsync(item => item.InvoiceId == invoice.Id && item.InvoiceType == SaleInvoiceType && !item.Deleted, cancellationToken);
        if (existing is not null)
        {
            if (!existing.IsActive)
            {
                existing.IsActive = true;
                existing.DisabledAt = null;
                existing.DisabledBy = null;
                existing.DisableReason = null;
            }

            existing.InvoiceNumber = invoice.InvoiceNumber;
            existing.InvoiceDate = invoice.OnDate;
            existing.CustomerName = invoice.CustomerName ?? "Walk-in Customer";
            existing.CustomerMobile = invoice.CustomerMobileNumber ?? string.Empty;
            existing.Amount = invoice.BillAmount;
            existing.StoreGroupId = store.StoreGroupId;
            existing.StoreId = invoice.StoreId;
            existing.CompanyId = invoice.CompanyId;
            existing.UpdatedAt = Now();
            await db.SaveChangesAsync(cancellationToken);
            return ToAction(existing, "Digital bill link is ready.");
        }

        var token = await GenerateUniqueTokenAsync(cancellationToken);
        var digitalInvoice = new DigitalInvoice
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoice.Id,
            InvoiceType = SaleInvoiceType,
            InvoiceNumber = invoice.InvoiceNumber,
            InvoiceDate = invoice.OnDate,
            CustomerName = invoice.CustomerName ?? "Walk-in Customer",
            CustomerMobile = invoice.CustomerMobileNumber ?? string.Empty,
            Amount = invoice.BillAmount,
            PublicToken = token,
            PublicUrl = BuildPublicPath(token),
            ExpiresAt = Now().AddDays(GetRetentionDays()),
            IsActive = true,
            WhatsAppStatus = "NotSent",
            CompanyId = invoice.CompanyId,
            StoreGroupId = store.StoreGroupId,
            StoreId = invoice.StoreId,
            CreatedBy = ResolveActor(context),
            CreatedAt = Now(),
            UpdatedAt = Now()
        };

        db.DigitalInvoices.Add(digitalInvoice);
        await db.SaveChangesAsync(cancellationToken);
        return ToAction(digitalInvoice, "Digital bill link generated.");
    }

    public async Task<DigitalBillActionResponseDto?> DisableAsync(Guid id, string? reason, HttpContext context, CancellationToken cancellationToken)
    {
        var item = await WorkspaceScope.ApplyTo(db.DigitalInvoices, context)
            .FirstOrDefaultAsync(entity => entity.Id == id && !entity.Deleted, cancellationToken);
        if (item is null)
        {
            return null;
        }

        item.IsActive = false;
        item.DisabledAt = Now();
        item.DisabledBy = ResolveActor(context);
        item.DisableReason = string.IsNullOrWhiteSpace(reason) ? "Disabled from Digital Bills admin page." : reason.Trim();
        item.UpdatedAt = Now();
        await db.SaveChangesAsync(cancellationToken);
        return ToAction(item, "Digital bill link disabled.");
    }

    public async Task<DigitalBillActionResponseDto?> RegenerateTokenAsync(Guid id, HttpContext context, CancellationToken cancellationToken)
    {
        var item = await WorkspaceScope.ApplyTo(db.DigitalInvoices, context)
            .FirstOrDefaultAsync(entity => entity.Id == id && !entity.Deleted, cancellationToken);
        if (item is null)
        {
            return null;
        }

        item.PublicToken = await GenerateUniqueTokenAsync(cancellationToken);
        item.PublicUrl = BuildPublicPath(item.PublicToken);
        item.IsActive = true;
        item.DisabledAt = null;
        item.DisabledBy = null;
        item.DisableReason = null;
        item.UpdatedAt = Now();
        await db.SaveChangesAsync(cancellationToken);
        return ToAction(item, "Digital bill token regenerated. Old public link is no longer valid.");
    }

    public async Task<PublicDigitalInvoiceDto?> LoadPublicInvoiceAsync(string token, HttpContext context, CancellationToken cancellationToken)
    {
        var item = await db.DigitalInvoices
            .FirstOrDefaultAsync(entity => entity.PublicToken == token && entity.IsActive && !entity.Deleted, cancellationToken);
        if (item is null || IsExpired(item))
        {
            return null;
        }

        var invoice = await db.SalesInvoices.AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == item.InvoiceId && !entity.Deleted, cancellationToken);
        if (invoice is null)
        {
            return null;
        }

        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == invoice.CompanyId, cancellationToken);
        var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == invoice.StoreId, cancellationToken);
        var review = await db.StoreReviewSettings.AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.CompanyId == invoice.CompanyId && entity.StoreId == invoice.StoreId && !entity.Deleted, cancellationToken);

        var now = Now();
        var storeGroupId = store?.StoreGroupId ?? Guid.Empty;
        var banners = await db.InvoiceAdBanners.AsNoTracking()
            .Where(entity => entity.CompanyId == invoice.CompanyId && entity.IsActive && !entity.Deleted)
            .Where(entity => entity.StoreId == null || entity.StoreId == invoice.StoreId)
            .Where(entity => entity.StoreGroupId == null || entity.StoreGroupId == storeGroupId)
            .Where(entity => entity.StartDate == null || entity.StartDate <= now)
            .Where(entity => entity.EndDate == null || entity.EndDate >= now)
            .OrderByDescending(entity => entity.Priority)
            .ThenBy(entity => entity.Position)
            .Take(6)
            .Select(entity => new PublicAdBannerDto(entity.Id, entity.Title, entity.ImageUrl, entity.TargetUrl, entity.Position))
            .ToListAsync(cancellationToken);

        var items = await db.InvoiceItems.AsNoTracking()
            .Include(entity => entity.Product)
            .Where(entity => entity.InvoiceId == invoice.Id)
            .OrderBy(entity => entity.CreatedAt)
            .Select(entity => new PublicInvoiceItemDto(
                entity.ProductName ?? (entity.Product != null ? entity.Product.Name : entity.Barcode),
                entity.Barcode,
                entity.BilledQuantity,
                entity.MRP,
                entity.DiscountAmount,
                entity.TaxPercentage,
                entity.TaxAmount,
                entity.HSNCode ?? (entity.Product != null ? entity.Product.HSNCode : null),
                entity.Unit.HasValue ? entity.Unit.Value.ToString() : null,
                entity.Amount))
            .ToListAsync(cancellationToken);

        await TrackEventAsync(item, "Opened", context, "PublicInvoice", null, null, cancellationToken);

        return new PublicDigitalInvoiceDto(
            item.Id,
            invoice.InvoiceNumber,
            invoice.OnDate,
            invoice.InvoiceStatus.ToString(),
            company?.Name ?? "Garmetix",
            FormatAddress(company?.Address, company?.City, company?.State, company?.ZipCode),
            company?.ContactNumber ?? string.Empty,
            company?.GSTIN ?? string.Empty,
            store?.Name ?? "Store",
            invoice.CustomerName ?? item.CustomerName,
            MaskMobile(invoice.CustomerMobileNumber),
            invoice.MRP,
            invoice.DiscountAmount,
            invoice.NetAmount,
            invoice.TaxAmount,
            invoice.RoundOff,
            invoice.BillAmount,
            invoice.PaidAmount,
            invoice.BalanceAmount,
            item.PublicToken,
            $"/api/public/digital-bills/{Uri.EscapeDataString(item.PublicToken)}/pdf",
            BuildGoodsReturnPolicyUrl(context),
            item.ExpiresAt,
            ToPublicReview(review),
            items,
            banners);
    }

    public async Task<byte[]?> BuildPublicInvoicePdfAsync(string token, HttpContext context, string? format, string? copy, bool? reprint, bool? signatures, CancellationToken cancellationToken)
    {
        var item = await db.DigitalInvoices
            .FirstOrDefaultAsync(entity => entity.PublicToken == token && entity.IsActive && !entity.Deleted, cancellationToken);
        if (item is null || IsExpired(item))
        {
            return null;
        }

        var invoice = await db.SalesInvoices.AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == item.InvoiceId && !entity.Deleted, cancellationToken);
        if (invoice is null)
        {
            return null;
        }

        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == invoice.CompanyId, cancellationToken);
        var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == invoice.StoreId, cancellationToken);
        var items = await db.InvoiceItems.AsNoTracking()
            .Include(entity => entity.Product)
            .Where(entity => entity.InvoiceId == invoice.Id)
            .OrderBy(entity => entity.CreatedAt)
            .Select(entity => new ReceiptItemDto(
                entity.Id,
                entity.ProductId,
                entity.ProductName ?? (entity.Product != null ? entity.Product.Name : entity.Barcode),
                entity.Barcode,
                entity.BilledQuantity,
                entity.MRP,
                entity.DiscountAmount,
                entity.TaxPercentage,
                entity.TaxAmount,
                entity.CGSTAmount,
                entity.SGSTAmount,
                entity.IGSTAmount,
                entity.HSNCode ?? (entity.Product != null ? entity.Product.HSNCode : null),
                entity.Unit.HasValue ? entity.Unit.Value.ToString() : null,
                entity.Amount))
            .ToListAsync(cancellationToken);

        var payments = await db.InvoicePayments.AsNoTracking()
            .Where(entity => entity.InvoiceId == invoice.Id)
            .OrderBy(entity => entity.OnDate)
            .Select(entity => new ReceiptPaymentDto(
                entity.Id,
                entity.OnDate,
                entity.Amount,
                entity.PaymentMode.ToString(),
                entity.ReferenceNumber,
                entity.GatewayReference,
                entity.SettlementStatus,
                entity.AdjustmentSourceType))
            .ToListAsync(cancellationToken);

        var model = new InvoicePdfModel(
            company?.Name ?? "Garmetix",
            FormatAddress(company?.Address, company?.City, company?.State, company?.ZipCode),
            company?.ContactNumber ?? string.Empty,
            company?.GSTIN ?? string.Empty,
            store?.Name ?? "Store",
            invoice.InvoiceNumber,
            invoice.OnDate,
            invoice.InvoiceStatus.ToString(),
            invoice.CustomerName ?? "Walk-in Customer",
            invoice.CustomerMobileNumber,
            invoice.MRP,
            invoice.DiscountAmount,
            invoice.NetAmount,
            invoice.TaxAmount,
            invoice.RoundOff,
            invoice.BillAmount,
            invoice.PaidAmount,
            invoice.BalanceAmount,
            items,
            payments,
            Garmetix.Api.ProductLookup.DocumentCodeService.Create(Garmetix.Api.ProductLookup.DocumentCodeService.SaleInvoice, invoice.Id),
            PolicyUrl: BuildGoodsReturnPolicyUrl(context));

        var pdf = InvoicePdfDocument.Build(model, format ?? "a4", copy ?? "customer", reprint == true, signatures != false);
        await TrackEventAsync(item, "PdfDownloaded", context, "PublicInvoice", null, null, cancellationToken);
        return pdf;
    }

    public async Task<bool> TrackPublicEventAsync(string token, DigitalBillEventRequest request, HttpContext context, CancellationToken cancellationToken)
    {
        var item = await db.DigitalInvoices.FirstOrDefaultAsync(entity => entity.PublicToken == token && entity.IsActive && !entity.Deleted, cancellationToken);
        if (item is null || IsExpired(item))
        {
            return false;
        }

        var eventType = NormalizeEventType(request.EventType);
        if (eventType == "BannerClicked" && Guid.TryParse(request.Source, out var bannerId))
        {
            var banner = await db.InvoiceAdBanners
                .FirstOrDefaultAsync(entity => entity.Id == bannerId && entity.CompanyId == item.CompanyId && !entity.Deleted, cancellationToken);
            if (banner is not null)
            {
                banner.ClickCount += 1;
                banner.UpdatedAt = Now();
            }
        }

        await TrackEventAsync(item, eventType, context, request.Source, request.TargetUrl, request.DetailsJson, cancellationToken);
        return true;
    }

    public async Task<CustomerFeedbackDto?> SubmitFeedbackAsync(string token, CustomerFeedbackSubmitRequest request, HttpContext context, CancellationToken cancellationToken)
    {
        var item = await db.DigitalInvoices.FirstOrDefaultAsync(entity => entity.PublicToken == token && entity.IsActive && !entity.Deleted, cancellationToken);
        if (item is null || IsExpired(item))
        {
            return null;
        }

        var rating = Math.Clamp(request.Rating, 1, 5);
        var feedback = new CustomerFeedback
        {
            Id = Guid.NewGuid(),
            DigitalInvoiceId = item.Id,
            CustomerName = item.CustomerName,
            CustomerMobile = item.CustomerMobile,
            Rating = rating,
            Message = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim(),
            Source = "DigitalInvoice",
            SubmittedAt = Now(),
            CompanyId = item.CompanyId,
            StoreGroupId = item.StoreGroupId,
            StoreId = item.StoreId,
            CreatedAt = Now(),
            UpdatedAt = Now()
        };
        db.CustomerFeedback.Add(feedback);
        item.FeedbackCount += 1;
        await TrackEventAsync(item, "FeedbackSubmitted", context, "PublicInvoice", null, null, cancellationToken, saveChanges: false);
        await db.SaveChangesAsync(cancellationToken);
        return new CustomerFeedbackDto(feedback.Id, feedback.DigitalInvoiceId, item.InvoiceNumber, feedback.CustomerName, MaskMobile(feedback.CustomerMobile), feedback.Rating, feedback.Message, feedback.SubmittedAt);
    }

    private async Task TrackEventAsync(DigitalInvoice item, string eventType, HttpContext context, string? source, string? targetUrl, string? detailsJson, CancellationToken cancellationToken, bool saveChanges = true)
    {
        switch (eventType)
        {
            case "Opened":
                item.OpenCount += 1;
                item.LastOpenedAt = Now();
                break;
            case "PdfDownloaded":
                item.PdfDownloadCount += 1;
                break;
            case "ReviewClicked":
                item.ReviewClickCount += 1;
                break;
        }

        item.UpdatedAt = Now();
        db.DigitalInvoiceEvents.Add(new DigitalInvoiceEvent
        {
            Id = Guid.NewGuid(),
            DigitalInvoiceId = item.Id,
            EventType = eventType,
            Source = source,
            TargetUrl = targetUrl,
            DetailsJson = detailsJson,
            EventAt = Now(),
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context.Request.Headers.UserAgent.ToString(),
            CompanyId = item.CompanyId,
            StoreGroupId = item.StoreGroupId,
            StoreId = item.StoreId,
            CreatedAt = Now(),
            UpdatedAt = Now()
        });

        if (saveChanges)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<string> GenerateUniqueTokenAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var bytes = RandomNumberGenerator.GetBytes(18);
            var token = Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
            var exists = await db.DigitalInvoices.AsNoTracking().AnyAsync(item => item.PublicToken == token, cancellationToken);
            if (!exists)
            {
                return token;
            }
        }

        return Guid.NewGuid().ToString("N");
    }

    private int GetRetentionDays() => Math.Clamp(configuration.GetValue<int?>("DigitalBills:RetentionDays") ?? 180, 1, 3650);

    private static bool IsExpired(DigitalInvoice item) => item.ExpiresAt.HasValue && item.ExpiresAt.Value < Now();

    private static string BuildPublicPath(string token) => $"/i/{Uri.EscapeDataString(token)}";

    private string BuildGoodsReturnPolicyUrl(HttpContext context)
    {
        var configuredBase = configuration["DigitalBills:PublicBaseUrl"]?.Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(configuredBase))
        {
            return $"{configuredBase}/goods-return-policy";
        }

        var scheme = string.IsNullOrWhiteSpace(context.Request.Scheme) ? "https" : context.Request.Scheme;
        var host = context.Request.Host.HasValue ? context.Request.Host.Value : "garmetix.aadwikafashion.in";
        return $"{scheme}://{host}/goods-return-policy";
    }

    private static DigitalBillActionResponseDto ToAction(DigitalInvoice item, string message)
        => new(item.Id, item.InvoiceId, item.InvoiceNumber, item.PublicToken, BuildPublicPath(item.PublicToken), item.ExpiresAt, item.IsActive, message);

    private static PublicReviewSettingDto ToPublicReview(StoreReviewSetting? setting)
        => new(
            setting?.EnableGoogleReview == true ? setting.GoogleReviewUrl : null,
            setting?.EnableInstagram == true ? setting.InstagramUrl : null,
            setting?.EnableFacebook == true ? setting.FacebookUrl : null,
            setting?.EnableWhatsappSupport == true ? setting.WhatsAppSupportNumber : null,
            setting?.EnableGoogleReview ?? false,
            setting?.EnableInstagram ?? false,
            setting?.EnableFacebook ?? false,
            setting?.EnableWhatsappSupport ?? false,
            setting?.EnablePrivateFeedback ?? true,
            string.IsNullOrWhiteSpace(setting?.ReviewButtonText) ? "Share your honest Google review" : setting!.ReviewButtonText!,
            string.IsNullOrWhiteSpace(setting?.FeedbackButtonText) ? "Share private feedback" : setting!.FeedbackButtonText!);

    private static string NormalizeEventType(string? value)
    {
        var normalized = Regex.Replace(value ?? string.Empty, @"[^A-Za-z0-9]", string.Empty);
        return normalized switch
        {
            "PdfDownloaded" => "PdfDownloaded",
            "ReviewClicked" => "ReviewClicked",
            "InstagramClicked" => "InstagramClicked",
            "FacebookClicked" => "FacebookClicked",
            "WhatsappSupportClicked" => "WhatsappSupportClicked",
            "BannerClicked" => "BannerClicked",
            _ => "CustomEvent"
        };
    }

    private static string ResolveActor(HttpContext context)
        => context.User.Identity?.Name
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
            ?? context.User.FindFirst("userName")?.Value
            ?? "System";

    private static string FormatAddress(params string?[] parts)
        => string.Join(", ", parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()));

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

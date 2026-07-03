using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Garmetix.Api.Workspace;
using Garmetix.Core.Models.Marketing;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Marketing;

public sealed class DigitalBillWhatsAppService(
    GarmetixDbContext db,
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    ILogger<DigitalBillWhatsAppService> logger)
{
    private const string ManualOnlyProvider = "ManualOnly";
    private const string MetaCloudProvider = "MetaCloudApi";

    public async Task<IReadOnlyList<WhatsAppProviderSettingSafeDto>> ListSettingsAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var rows = await WorkspaceScope.ApplyTo(db.WhatsAppProviderSettings.AsNoTracking(), context)
            .Where(item => !item.Deleted)
            .OrderBy(item => item.StoreId)
            .ToListAsync(cancellationToken);

        return rows.Select(ToSafeDto).ToList();
    }

    public async Task<WhatsAppProviderSettingSafeDto?> UpsertSettingAsync(Guid storeId, WhatsAppProviderSettingDto request, HttpContext context, CancellationToken cancellationToken)
    {
        var store = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .Where(item => item.Id == storeId && item.CompanyId == request.CompanyId)
            .Select(item => new { item.Id, item.CompanyId, item.StoreGroupId })
            .FirstOrDefaultAsync(cancellationToken);
        if (store is null)
        {
            return null;
        }

        var item = await db.WhatsAppProviderSettings
            .FirstOrDefaultAsync(entity => entity.CompanyId == store.CompanyId && entity.StoreId == store.Id && !entity.Deleted, cancellationToken);
        if (item is null)
        {
            item = new WhatsAppProviderSetting
            {
                Id = Guid.NewGuid(),
                CompanyId = store.CompanyId,
                StoreGroupId = store.StoreGroupId,
                StoreId = store.Id,
                CreatedAt = Now(),
                CreatedBy = ResolveActor(context)
            };
            db.WhatsAppProviderSettings.Add(item);
        }

        item.CompanyId = store.CompanyId;
        item.StoreGroupId = store.StoreGroupId;
        item.StoreId = store.Id;
        item.IsEnabled = request.IsEnabled;
        item.AutoSendDigitalBills = request.AutoSendDigitalBills;
        item.Provider = NormalizeProvider(request.Provider);
        item.ApiBaseUrl = CleanNullable(request.ApiBaseUrl);
        if (!string.IsNullOrWhiteSpace(request.ApiToken))
        {
            item.ApiToken = request.ApiToken.Trim();
        }
        item.PhoneNumberId = CleanNullable(request.PhoneNumberId);
        item.SenderId = CleanNullable(request.SenderId);
        item.TemplateName = CleanNullable(request.TemplateName);
        item.LanguageCode = string.IsNullOrWhiteSpace(request.LanguageCode) ? "en" : request.LanguageCode.Trim();
        item.MessageTemplateText = string.IsNullOrWhiteSpace(request.MessageTemplateText)
            ? DefaultTemplate()
            : request.MessageTemplateText.Trim();
        item.SendPdfLink = request.SendPdfLink;
        item.FallbackToManualLog = request.FallbackToManualLog;
        item.RetryLimit = Math.Clamp(request.RetryLimit, 0, 10);
        item.UpdatedAt = Now();

        await db.SaveChangesAsync(cancellationToken);
        return ToSafeDto(item);
    }

    public async Task<WhatsAppSendResultDto?> TestSendAsync(WhatsAppTestSendRequest request, HttpContext context, CancellationToken cancellationToken)
    {
        var setting = await ResolveSettingAsync(context, request.StoreId, cancellationToken);
        if (setting is null)
        {
            return null;
        }

        var storeName = await db.Stores.AsNoTracking()
            .Where(item => item.Id == request.StoreId)
            .Select(item => item.Name ?? "Garmetix Store")
            .FirstOrDefaultAsync(cancellationToken) ?? "Garmetix Store";

        var message = string.IsNullOrWhiteSpace(request.Message)
            ? ("Test WhatsApp from " + storeName + ". Digital Bill CRM is configured.")
            : request.Message.Trim();

        var log = new WhatsAppMessageLog
        {
            Id = Guid.NewGuid(),
            CompanyId = setting.CompanyId,
            StoreGroupId = setting.StoreGroupId,
            StoreId = setting.StoreId,
            CustomerMobile = NormalizeMobileForStorage(request.CustomerMobile),
            Provider = setting.Provider,
            TemplateName = setting.TemplateName,
            MessageBody = message,
            Status = "Queued",
            CreatedAt = Now(),
            CreatedBy = ResolveActor(context)
        };

        db.WhatsAppMessageLogs.Add(log);
        await db.SaveChangesAsync(cancellationToken);

        var result = await DispatchAsync(log, setting, request.CustomerMobile, request.CustomerName ?? "Customer", storeName, null, null, null, context, cancellationToken);
        setting.LastTestAt = Now();
        setting.LastError = result.ErrorMessage;
        setting.UpdatedAt = Now();
        await db.SaveChangesAsync(cancellationToken);
        return result;
    }



    public async Task<DigitalBillCampaignSendResultDto?> SendMarketingCampaignAsync(Guid campaignId, DigitalBillCampaignSendRequest request, HttpContext context, CancellationToken cancellationToken)
    {
        var campaign = await WorkspaceScope.ApplyTo(db.DigitalBillCampaigns, context)
            .FirstOrDefaultAsync(item => item.Id == campaignId && !item.Deleted, cancellationToken);
        if (campaign is null)
        {
            return null;
        }

        if (campaign.Status == "Cancelled")
        {
            return new DigitalBillCampaignSendResultDto(campaign.Id, campaign.Name, campaign.Status, 0, 0, 0, 0, 0, "Cancelled campaigns cannot be sent.", Array.Empty<DigitalBillCampaignSendRecipientResultDto>());
        }

        var limit = Math.Clamp(request.Limit <= 0 ? 500 : request.Limit, 1, 2000);
        var now = Now();
        var recipients = await WorkspaceScope.ApplyTo(db.DigitalBillCampaignRecipients, context)
            .Where(item => item.CampaignId == campaignId && !item.Deleted)
            .OrderBy(item => item.CustomerName)
            .ThenBy(item => item.CustomerMobile)
            .Take(limit)
            .ToListAsync(cancellationToken);

        if (!request.Force)
        {
            recipients = recipients
                .Where(item => item.Status is "Prepared" or "ManualPending" or "Failed" or "Queued" or "Draft")
                .ToList();
        }

        if (recipients.Count == 0)
        {
            return new DigitalBillCampaignSendResultDto(campaign.Id, campaign.Name, campaign.Status, 0, 0, 0, 0, 0, "No pending campaign recipients were found.", Array.Empty<DigitalBillCampaignSendRecipientResultDto>());
        }

        if (campaign.Channel != "WhatsAppProvider" && campaign.Channel != "WhatsAppMarketingTemplate")
        {
            foreach (var recipient in recipients)
            {
                recipient.Status = "ManualPending";
                recipient.QueuedAt ??= now;
                recipient.ErrorMessage = "Campaign channel is manual. Copy/export messages or change channel to an approved WhatsApp marketing template campaign.";
                recipient.UpdatedAt = now;
            }

            campaign.Status = "Queued";
            campaign.QueuedAt ??= now;
            campaign.PreparedCount = 0;
            campaign.UpdatedAt = now;
            await db.SaveChangesAsync(cancellationToken);

            var rows = recipients.Select(item => new DigitalBillCampaignSendRecipientResultDto(
                item.Id,
                null,
                item.CustomerName,
                item.CustomerMobile,
                item.Status,
                null,
                item.ErrorMessage)).ToList();
            return BuildCampaignSendResult(campaign, rows, "Campaign is in manual/copy mode. Recipients were marked ManualPending.");
        }

        if (string.IsNullOrWhiteSpace(campaign.TemplateName))
        {
            foreach (var recipient in recipients)
            {
                recipient.Status = "ManualPending";
                recipient.QueuedAt ??= now;
                recipient.ErrorMessage = "Approved WhatsApp marketing template name is required before API sending.";
                recipient.UpdatedAt = now;
            }

            campaign.Status = "Queued";
            campaign.QueuedAt ??= now;
            campaign.PreparedCount = 0;
            campaign.UpdatedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            var rows = recipients.Select(item => new DigitalBillCampaignSendRecipientResultDto(item.Id, null, item.CustomerName, item.CustomerMobile, item.Status, null, item.ErrorMessage)).ToList();
            return BuildCampaignSendResult(campaign, rows, "Campaign template name is missing. Recipients were kept ManualPending.");
        }

        var storeIds = recipients.Select(item => item.StoreId).Where(id => id != Guid.Empty).Distinct().ToList();
        var storeNames = await db.Stores.AsNoTracking()
            .Where(item => storeIds.Contains(item.Id))
            .Select(item => new { item.Id, item.Name })
            .ToDictionaryAsync(item => item.Id, item => string.IsNullOrWhiteSpace(item.Name) ? "Garmetix Store" : item.Name, cancellationToken);

        campaign.Status = "Queued";
        campaign.QueuedAt ??= now;
        campaign.PreparedCount = 0;
        campaign.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);

        var results = new List<DigitalBillCampaignSendRecipientResultDto>();
        var settingCache = new Dictionary<Guid, WhatsAppProviderSetting?>();
        foreach (var recipient in recipients)
        {
            var mobileForSend = NormalizeMobileForSend(recipient.CustomerMobile);
            if (mobileForSend is null)
            {
                recipient.Status = "Skipped";
                recipient.ErrorMessage = "Customer mobile number is missing or invalid.";
                recipient.UpdatedAt = Now();
                results.Add(new DigitalBillCampaignSendRecipientResultDto(recipient.Id, null, recipient.CustomerName, recipient.CustomerMobile, recipient.Status, null, recipient.ErrorMessage));
                continue;
            }

            if (!settingCache.TryGetValue(recipient.StoreId, out var setting))
            {
                setting = await ResolveSettingAsync(context, recipient.StoreId, cancellationToken);
                settingCache[recipient.StoreId] = setting;
            }

            if (setting is null || !setting.IsEnabled)
            {
                recipient.Status = "ManualPending";
                recipient.QueuedAt ??= Now();
                recipient.ErrorMessage = setting is null
                    ? "WhatsApp provider is not configured for this store."
                    : "WhatsApp provider is disabled for this store.";
                recipient.UpdatedAt = Now();
                results.Add(new DigitalBillCampaignSendRecipientResultDto(recipient.Id, null, recipient.CustomerName, recipient.CustomerMobile, recipient.Status, null, recipient.ErrorMessage));
                continue;
            }

            if (!NormalizeProvider(setting.Provider).Equals(MetaCloudProvider, StringComparison.OrdinalIgnoreCase))
            {
                recipient.Status = setting.FallbackToManualLog ? "ManualPending" : "Failed";
                recipient.QueuedAt ??= Now();
                recipient.FailedAt = recipient.Status == "Failed" ? Now() : recipient.FailedAt;
                recipient.ErrorMessage = $"Provider '{setting.Provider}' is not implemented for marketing campaign API sending. Configure MetaCloudApi or use manual copy/export.";
                recipient.UpdatedAt = Now();
                results.Add(new DigitalBillCampaignSendRecipientResultDto(recipient.Id, null, recipient.CustomerName, recipient.CustomerMobile, recipient.Status, null, recipient.ErrorMessage));
                continue;
            }

            var storeName = storeNames.TryGetValue(recipient.StoreId, out var foundStoreName) ? foundStoreName : "Garmetix Store";
            var publicUrl = BuildAbsoluteUrl(recipient.PublicPath, context);
            var log = new WhatsAppMessageLog
            {
                Id = Guid.NewGuid(),
                DigitalInvoiceId = recipient.DigitalInvoiceId,
                CompanyId = recipient.CompanyId,
                StoreGroupId = recipient.StoreGroupId,
                StoreId = recipient.StoreId,
                CustomerMobile = NormalizeMobileForStorage(recipient.CustomerMobile),
                Provider = MetaCloudProvider,
                TemplateName = campaign.TemplateName,
                MessageBody = recipient.MessageBody,
                Status = "Queued",
                CreatedAt = Now(),
                CreatedBy = ResolveActor(context)
            };
            db.WhatsAppMessageLogs.Add(log);
            recipient.Status = "Queued";
            recipient.QueuedAt ??= Now();
            recipient.UpdatedAt = Now();
            await db.SaveChangesAsync(cancellationToken);

            var sent = await SendMetaCampaignTemplateAsync(log, setting, campaign, recipient, mobileForSend, storeName, publicUrl, cancellationToken);
            recipient.Status = sent.Status == "Sent" ? "Sent" : sent.Status == "ManualPending" ? "ManualPending" : sent.Status == "Skipped" ? "Skipped" : "Failed";
            recipient.SentAt = sent.Status == "Sent" ? sent.SentAt ?? Now() : recipient.SentAt;
            recipient.FailedAt = recipient.Status == "Failed" ? Now() : recipient.FailedAt;
            recipient.ErrorMessage = sent.ErrorMessage;
            recipient.UpdatedAt = Now();
            results.Add(new DigitalBillCampaignSendRecipientResultDto(recipient.Id, sent.LogId, recipient.CustomerName, recipient.CustomerMobile, recipient.Status, sent.ProviderMessageId, sent.ErrorMessage));
            await db.SaveChangesAsync(cancellationToken);
        }

        campaign.SentCount = await CountCampaignRecipientsAsync(context, campaignId, "Sent", cancellationToken);
        campaign.FailedCount = await CountCampaignRecipientsAsync(context, campaignId, "Failed", cancellationToken);
        campaign.PreparedCount = await CountCampaignRecipientsAsync(context, campaignId, "Prepared", cancellationToken);
        var pendingCount = await CountCampaignRecipientsAsync(context, campaignId, "ManualPending", cancellationToken);
        campaign.Status = campaign.FailedCount == 0 && campaign.PreparedCount == 0 && pendingCount == 0 ? "Completed" : "Queued";
        if (campaign.Status == "Completed")
        {
            campaign.CompletedAt ??= Now();
        }
        campaign.UpdatedAt = Now();
        await db.SaveChangesAsync(cancellationToken);

        return BuildCampaignSendResult(campaign, results, "WhatsApp marketing template send completed for this batch.");
    }

    public async Task<WhatsAppSendResultDto?> TryAutoSendForDigitalInvoiceAsync(Guid digitalInvoiceId, HttpContext context, CancellationToken cancellationToken)
    {
        var digitalInvoice = await WorkspaceScope.ApplyTo(db.DigitalInvoices, context)
            .FirstOrDefaultAsync(item => item.Id == digitalInvoiceId && !item.Deleted, cancellationToken);
        if (digitalInvoice is null)
        {
            return null;
        }

        var setting = await ResolveSettingAsync(context, digitalInvoice.StoreId, cancellationToken);
        if (setting is null)
        {
            digitalInvoice.WhatsAppStatus = "NotConfigured";
            digitalInvoice.UpdatedAt = Now();
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }

        if (!setting.IsEnabled)
        {
            digitalInvoice.WhatsAppStatus = "Disabled";
            digitalInvoice.UpdatedAt = Now();
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }

        if (!setting.AutoSendDigitalBills)
        {
            digitalInvoice.WhatsAppStatus = "ManualPending";
            digitalInvoice.UpdatedAt = Now();
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }

        return await SendForDigitalInvoiceAsync(digitalInvoiceId, context, cancellationToken, force: false);
    }

    public async Task<WhatsAppSendResultDto?> SendForDigitalInvoiceAsync(Guid digitalInvoiceId, HttpContext context, CancellationToken cancellationToken, bool force = true)
    {
        var digitalInvoice = await WorkspaceScope.ApplyTo(db.DigitalInvoices, context)
            .FirstOrDefaultAsync(item => item.Id == digitalInvoiceId && !item.Deleted, cancellationToken);
        if (digitalInvoice is null)
        {
            return null;
        }

        var setting = await ResolveSettingAsync(context, digitalInvoice.StoreId, cancellationToken);
        if (setting is null)
        {
            return await CreateFinalLogAsync(digitalInvoice, ManualOnlyProvider, null, BuildDefaultMessage(digitalInvoice, "Garmetix Store", BuildPublicUrl(digitalInvoice, context)), "NotConfigured", "WhatsApp provider is not configured for this store.", context, cancellationToken);
        }

        if (!setting.IsEnabled && !force)
        {
            digitalInvoice.WhatsAppStatus = "Disabled";
            digitalInvoice.UpdatedAt = Now();
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }

        var storeName = await db.Stores.AsNoTracking()
            .Where(item => item.Id == digitalInvoice.StoreId)
            .Select(item => item.Name ?? "Garmetix Store")
            .FirstOrDefaultAsync(cancellationToken) ?? "Garmetix Store";

        var fullUrl = BuildPublicUrl(digitalInvoice, context);
        var message = RenderTemplate(setting.MessageTemplateText, digitalInvoice, storeName, fullUrl);
        var normalizedMobile = NormalizeMobileForStorage(digitalInvoice.CustomerMobile);
        if (NormalizeMobileForSend(digitalInvoice.CustomerMobile) is null)
        {
            return await CreateFinalLogAsync(digitalInvoice, setting.Provider, setting.TemplateName, message, "Skipped", "Customer mobile number is missing or invalid.", context, cancellationToken);
        }

        var log = new WhatsAppMessageLog
        {
            Id = Guid.NewGuid(),
            DigitalInvoiceId = digitalInvoice.Id,
            CompanyId = digitalInvoice.CompanyId,
            StoreGroupId = digitalInvoice.StoreGroupId,
            StoreId = digitalInvoice.StoreId,
            CustomerMobile = normalizedMobile,
            Provider = setting.Provider,
            TemplateName = setting.TemplateName,
            MessageBody = message,
            Status = "Queued",
            CreatedAt = Now(),
            CreatedBy = ResolveActor(context)
        };
        db.WhatsAppMessageLogs.Add(log);
        digitalInvoice.WhatsAppStatus = "Queued";
        digitalInvoice.UpdatedAt = Now();
        await db.SaveChangesAsync(cancellationToken);

        var result = await DispatchAsync(log, setting, digitalInvoice.CustomerMobile, digitalInvoice.CustomerName, storeName, digitalInvoice, fullUrl, null, context, cancellationToken);
        digitalInvoice.WhatsAppStatus = result.Status;
        if (result.Status == "Sent")
        {
            digitalInvoice.LastWhatsAppSentAt = result.SentAt;
        }
        digitalInvoice.UpdatedAt = Now();
        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<WhatsAppSendResultDto?> RetryLogAsync(Guid logId, HttpContext context, CancellationToken cancellationToken)
    {
        var log = await WorkspaceScope.ApplyTo(db.WhatsAppMessageLogs, context)
            .FirstOrDefaultAsync(item => item.Id == logId && !item.Deleted, cancellationToken);
        if (log is null)
        {
            return null;
        }

        var setting = await ResolveSettingAsync(context, log.StoreId, cancellationToken);
        if (setting is null)
        {
            log.Status = "NotConfigured";
            log.ErrorMessage = "WhatsApp provider is not configured for this store.";
            log.UpdatedAt = Now();
            await db.SaveChangesAsync(cancellationToken);
            return ToResult(log);
        }

        if (log.RetryCount >= setting.RetryLimit)
        {
            log.Status = "RetryLimitReached";
            log.ErrorMessage = $"Retry limit {setting.RetryLimit} reached.";
            log.UpdatedAt = Now();
            await db.SaveChangesAsync(cancellationToken);
            return ToResult(log);
        }

        var digitalInvoice = log.DigitalInvoiceId.HasValue
            ? await db.DigitalInvoices.FirstOrDefaultAsync(item => item.Id == log.DigitalInvoiceId.Value && !item.Deleted, cancellationToken)
            : null;
        var storeName = await db.Stores.AsNoTracking()
            .Where(item => item.Id == log.StoreId)
            .Select(item => item.Name ?? "Garmetix Store")
            .FirstOrDefaultAsync(cancellationToken) ?? "Garmetix Store";
        var url = digitalInvoice is null ? null : BuildPublicUrl(digitalInvoice, context);

        log.RetryCount += 1;
        log.Status = "Queued";
        log.ErrorMessage = null;
        log.UpdatedAt = Now();
        await db.SaveChangesAsync(cancellationToken);

        var result = await DispatchAsync(log, setting, log.CustomerMobile, digitalInvoice?.CustomerName ?? "Customer", storeName, digitalInvoice, url, null, context, cancellationToken);
        if (digitalInvoice is not null)
        {
            digitalInvoice.WhatsAppStatus = result.Status;
            if (result.Status == "Sent")
            {
                digitalInvoice.LastWhatsAppSentAt = result.SentAt;
            }
            digitalInvoice.UpdatedAt = Now();
            await db.SaveChangesAsync(cancellationToken);
        }

        return result;
    }


    public async Task<WhatsAppWebhookResultDto> HandleMetaWebhookAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var statuses = ExtractMetaStatuses(payload).ToList();
        if (statuses.Count == 0)
        {
            return new WhatsAppWebhookResultDto(MetaCloudProvider, 0, 0, "Webhook accepted. No message status rows were present.");
        }

        var updated = 0;
        var ignored = 0;
        foreach (var status in statuses)
        {
            if (string.IsNullOrWhiteSpace(status.MessageId))
            {
                ignored++;
                continue;
            }

            var log = await db.WhatsAppMessageLogs
                .FirstOrDefaultAsync(item => item.ProviderMessageId == status.MessageId && !item.Deleted, cancellationToken);
            if (log is null)
            {
                ignored++;
                continue;
            }

            var normalizedStatus = NormalizeWebhookStatus(status.Status);
            log.Status = normalizedStatus;
            log.ErrorMessage = status.ErrorMessage;
            log.UpdatedAt = Now();
            var eventAt = status.EventAt ?? Now();
            if (normalizedStatus == "Sent")
            {
                log.SentAt ??= eventAt;
            }
            else if (normalizedStatus == "Delivered")
            {
                log.SentAt ??= eventAt;
                log.DeliveredAt = eventAt;
            }
            else if (normalizedStatus == "Read")
            {
                log.SentAt ??= eventAt;
                log.DeliveredAt ??= eventAt;
                log.ReadAt = eventAt;
            }

            if (log.DigitalInvoiceId.HasValue)
            {
                var digitalInvoice = await db.DigitalInvoices
                    .FirstOrDefaultAsync(item => item.Id == log.DigitalInvoiceId.Value && !item.Deleted, cancellationToken);
                if (digitalInvoice is not null)
                {
                    digitalInvoice.WhatsAppStatus = normalizedStatus;
                    if (normalizedStatus is "Sent" or "Delivered" or "Read")
                    {
                        digitalInvoice.LastWhatsAppSentAt ??= log.SentAt ?? eventAt;
                    }
                    digitalInvoice.UpdatedAt = Now();
                }
            }

            updated++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new WhatsAppWebhookResultDto(MetaCloudProvider, updated, ignored, $"Webhook accepted. Updated {updated} message log(s), ignored {ignored} status row(s).");
    }

    private async Task<WhatsAppSendResultDto> DispatchAsync(
        WhatsAppMessageLog log,
        WhatsAppProviderSetting setting,
        string mobile,
        string customerName,
        string storeName,
        DigitalInvoice? digitalInvoice,
        string? fullUrl,
        string? overrideMessage,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        log.Provider = NormalizeProvider(setting.Provider);
        log.TemplateName = setting.TemplateName;
        log.MessageBody = overrideMessage ?? log.MessageBody;
        log.UpdatedAt = Now();

        if (log.Provider == ManualOnlyProvider)
        {
            log.Status = "ManualPending";
            log.ErrorMessage = "ManualOnly provider selected. Copy/send this message manually or configure an API provider.";
            await db.SaveChangesAsync(cancellationToken);
            return ToResult(log);
        }

        var normalizedDestination = NormalizeMobileForSend(mobile);
        if (normalizedDestination is null)
        {
            log.Status = "Skipped";
            log.ErrorMessage = "Customer mobile number is missing or invalid.";
            await db.SaveChangesAsync(cancellationToken);
            return ToResult(log);
        }

        if (!setting.IsEnabled)
        {
            log.Status = setting.FallbackToManualLog ? "ManualPending" : "Disabled";
            log.ErrorMessage = "WhatsApp provider is disabled for this store.";
            await db.SaveChangesAsync(cancellationToken);
            return ToResult(log);
        }

        try
        {
            if (log.Provider == MetaCloudProvider)
            {
                return await SendMetaCloudAsync(log, setting, normalizedDestination, customerName, storeName, digitalInvoice, fullUrl, cancellationToken);
            }

            log.Status = setting.FallbackToManualLog ? "ManualPending" : "Failed";
            log.ErrorMessage = $"Provider '{log.Provider}' is configured, but API dispatch is not implemented yet. Use ManualOnly or MetaCloudApi for now.";
            await db.SaveChangesAsync(cancellationToken);
            return ToResult(log);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Digital bill WhatsApp send failed for log {LogId}", log.Id);
            log.Status = setting.FallbackToManualLog ? "ManualPending" : "Failed";
            log.ErrorMessage = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
            log.UpdatedAt = Now();
            await db.SaveChangesAsync(cancellationToken);
            return ToResult(log);
        }
    }

    private async Task<WhatsAppSendResultDto> SendMetaCloudAsync(
        WhatsAppMessageLog log,
        WhatsAppProviderSetting setting,
        string destination,
        string customerName,
        string storeName,
        DigitalInvoice? digitalInvoice,
        string? fullUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(setting.ApiToken) || string.IsNullOrWhiteSpace(setting.PhoneNumberId))
        {
            log.Status = setting.FallbackToManualLog ? "ManualPending" : "Failed";
            log.ErrorMessage = "Meta Cloud API token or phone number ID is missing.";
            await db.SaveChangesAsync(cancellationToken);
            return ToResult(log);
        }

        var baseUrl = ResolveMetaCloudApiBaseUrl(setting.ApiBaseUrl);
        var endpoint = $"{baseUrl}/{setting.PhoneNumberId}/messages";

        object payload = string.IsNullOrWhiteSpace(setting.TemplateName)
            ? new
            {
                messaging_product = "whatsapp",
                to = destination,
                type = "text",
                text = new { preview_url = true, body = log.MessageBody }
            }
            : new
            {
                messaging_product = "whatsapp",
                to = destination,
                type = "template",
                template = new
                {
                    name = setting.TemplateName,
                    language = new { code = string.IsNullOrWhiteSpace(setting.LanguageCode) ? "en" : setting.LanguageCode },
                    components = new[]
                    {
                        new
                        {
                            type = "body",
                            // Aadwika/Garmetix invoice template uses 4 Meta body variables:
                            // {{1}} customer name, {{2}} invoice number, {{3}} amount, {{4}} public bill URL.
                            // Store name is fixed in the approved Meta template text, because Meta UI may disable Save
                            // when too many variables are used in a short utility template.
                            parameters = new[]
                            {
                                new { type = "text", text = MetaText(customerName, "Customer") },
                                new { type = "text", text = MetaText(digitalInvoice?.InvoiceNumber, "TEST") },
                                new { type = "text", text = MetaText(digitalInvoice?.Amount.ToString("0.##"), "0") },
                                new { type = "text", text = MetaText(fullUrl, ResolveFallbackDigitalBillUrl()) }
                            }
                        }
                    }
                }
            };

        var json = JsonSerializer.Serialize(payload);
        var client = httpClientFactory.CreateClient("DigitalBillWhatsApp");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", setting.ApiToken.Trim());
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            log.Status = setting.FallbackToManualLog ? "ManualPending" : "Failed";
            log.ErrorMessage = $"Meta Cloud API returned {(int)response.StatusCode}: {Trim(responseBody, 900)}";
            log.UpdatedAt = Now();
            await db.SaveChangesAsync(cancellationToken);
            return ToResult(log);
        }

        log.Status = "Sent";
        log.SentAt = Now();
        log.ErrorMessage = null;
        log.ProviderMessageId = ExtractMetaMessageId(responseBody);
        log.UpdatedAt = Now();
        await db.SaveChangesAsync(cancellationToken);
        return ToResult(log);
    }


    private async Task<WhatsAppSendResultDto> SendMetaCampaignTemplateAsync(
        WhatsAppMessageLog log,
        WhatsAppProviderSetting setting,
        DigitalBillCampaign campaign,
        DigitalBillCampaignRecipient recipient,
        string destination,
        string storeName,
        string publicUrl,
        CancellationToken cancellationToken)
    {
        log.Provider = MetaCloudProvider;
        log.TemplateName = campaign.TemplateName;
        log.UpdatedAt = Now();

        if (string.IsNullOrWhiteSpace(setting.ApiToken) || string.IsNullOrWhiteSpace(setting.PhoneNumberId))
        {
            log.Status = setting.FallbackToManualLog ? "ManualPending" : "Failed";
            log.ErrorMessage = "Meta Cloud API token or phone number ID is missing.";
            log.UpdatedAt = Now();
            await db.SaveChangesAsync(cancellationToken);
            return ToResult(log);
        }

        if (string.IsNullOrWhiteSpace(campaign.TemplateName))
        {
            log.Status = "ManualPending";
            log.ErrorMessage = "Approved WhatsApp marketing template name is required before API sending.";
            log.UpdatedAt = Now();
            await db.SaveChangesAsync(cancellationToken);
            return ToResult(log);
        }

        var baseUrl = ResolveMetaCloudApiBaseUrl(setting.ApiBaseUrl);
        var endpoint = $"{baseUrl}/{setting.PhoneNumberId}/messages";
        var offerUrl = string.IsNullOrWhiteSpace(campaign.OfferUrl) ? publicUrl : campaign.OfferUrl.Trim();
        var amount = recipient.LastInvoiceAmount.ToString("0.##");

        var payload = new
        {
            messaging_product = "whatsapp",
            to = destination,
            type = "template",
            template = new
            {
                name = campaign.TemplateName.Trim(),
                language = new { code = string.IsNullOrWhiteSpace(setting.LanguageCode) ? "en" : setting.LanguageCode.Trim() },
                components = new[]
                {
                    new
                    {
                        type = "body",
                        parameters = new[]
                        {
                            new { type = "text", text = MetaText(recipient.CustomerName, "Customer") },
                            new { type = "text", text = MetaText(storeName, "Garmetix Store") },
                            new { type = "text", text = MetaText(recipient.InvoiceNumber, "Invoice") },
                            new { type = "text", text = MetaText(amount, "0") },
                            new { type = "text", text = MetaText(publicUrl, ResolveFallbackDigitalBillUrl()) },
                            new { type = "text", text = MetaText(offerUrl, publicUrl, ResolveFallbackDigitalBillUrl()) }
                        }
                    }
                }
            }
        };

        try
        {
            var json = JsonSerializer.Serialize(payload);
            var client = httpClientFactory.CreateClient("DigitalBillWhatsApp");
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", setting.ApiToken.Trim());
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                log.Status = setting.FallbackToManualLog ? "ManualPending" : "Failed";
                log.ErrorMessage = $"Meta Cloud API returned {(int)response.StatusCode}: {Trim(responseBody, 900)}";
                log.UpdatedAt = Now();
                await db.SaveChangesAsync(cancellationToken);
                return ToResult(log);
            }

            log.Status = "Sent";
            log.SentAt = Now();
            log.ErrorMessage = null;
            log.ProviderMessageId = ExtractMetaMessageId(responseBody);
            log.UpdatedAt = Now();
            await db.SaveChangesAsync(cancellationToken);
            return ToResult(log);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Digital bill campaign WhatsApp template send failed for log {LogId}", log.Id);
            log.Status = setting.FallbackToManualLog ? "ManualPending" : "Failed";
            log.ErrorMessage = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
            log.UpdatedAt = Now();
            await db.SaveChangesAsync(cancellationToken);
            return ToResult(log);
        }
    }

    private string ResolveFallbackDigitalBillUrl()
    {
        var configuredBase = configuration["DigitalBills:PublicBaseUrl"]?.Trim().TrimEnd('/');
        return string.IsNullOrWhiteSpace(configuredBase) ? "https://garmetix.aadwikafashion.in" : configuredBase;
    }

    private static string MetaText(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var value = candidate?.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return "-";
    }

    private string ResolveMetaCloudApiBaseUrl(string? configuredBaseUrl)
    {
        var defaultBaseUrl = configuration["WhatsApp:MetaCloudApiBaseUrl"]?.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(defaultBaseUrl))
        {
            defaultBaseUrl = "https://graph.facebook.com/v20.0";
        }

        var candidate = string.IsNullOrWhiteSpace(configuredBaseUrl)
            ? defaultBaseUrl
            : configuredBaseUrl.Trim().TrimEnd('/');

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(uri.Host)
            || !uri.Host.Equals("graph.facebook.com", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Ignoring invalid Meta Cloud API base URL '{ConfiguredBaseUrl}'. Falling back to {DefaultBaseUrl}.", configuredBaseUrl, defaultBaseUrl);
            return defaultBaseUrl;
        }

        if (string.IsNullOrWhiteSpace(uri.AbsolutePath) || uri.AbsolutePath == "/")
        {
            return defaultBaseUrl;
        }

        return candidate;
    }

    private async Task<int> CountCampaignRecipientsAsync(HttpContext context, Guid campaignId, string status, CancellationToken cancellationToken)
        => await WorkspaceScope.ApplyTo(db.DigitalBillCampaignRecipients.AsNoTracking(), context)
            .CountAsync(item => item.CampaignId == campaignId && item.Status == status && !item.Deleted, cancellationToken);

    private DigitalBillCampaignSendResultDto BuildCampaignSendResult(
        DigitalBillCampaign campaign,
        IReadOnlyList<DigitalBillCampaignSendRecipientResultDto> recipients,
        string message)
        => new(
            campaign.Id,
            campaign.Name,
            campaign.Status,
            recipients.Count,
            recipients.Count(item => item.Status == "Sent"),
            recipients.Count(item => item.Status == "ManualPending"),
            recipients.Count(item => item.Status == "Failed"),
            recipients.Count(item => item.Status == "Skipped"),
            message,
            recipients);

    private string BuildAbsoluteUrl(string? publicPath, HttpContext context)
    {
        var path = string.IsNullOrWhiteSpace(publicPath) ? "/" : publicPath.Trim();
        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        var configuredBase = configuration["DigitalBills:PublicBaseUrl"]?.Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(configuredBase))
        {
            return configuredBase + path;
        }

        var forwardedProto = context.Request.Headers["X-Forwarded-Proto"].FirstOrDefault();
        var forwardedHost = context.Request.Headers["X-Forwarded-Host"].FirstOrDefault();
        var scheme = string.IsNullOrWhiteSpace(forwardedProto) ? context.Request.Scheme : forwardedProto;
        var host = string.IsNullOrWhiteSpace(forwardedHost) ? context.Request.Host.Value : forwardedHost;
        return $"{scheme}://{host}{path}";
    }

    private async Task<WhatsAppSendResultDto> CreateFinalLogAsync(DigitalInvoice digitalInvoice, string provider, string? templateName, string message, string status, string error, HttpContext context, CancellationToken cancellationToken)
    {
        var log = new WhatsAppMessageLog
        {
            Id = Guid.NewGuid(),
            DigitalInvoiceId = digitalInvoice.Id,
            CompanyId = digitalInvoice.CompanyId,
            StoreGroupId = digitalInvoice.StoreGroupId,
            StoreId = digitalInvoice.StoreId,
            CustomerMobile = NormalizeMobileForStorage(digitalInvoice.CustomerMobile),
            Provider = provider,
            TemplateName = templateName,
            MessageBody = message,
            Status = status,
            ErrorMessage = error,
            CreatedAt = Now(),
            CreatedBy = ResolveActor(context)
        };
        db.WhatsAppMessageLogs.Add(log);
        digitalInvoice.WhatsAppStatus = status;
        digitalInvoice.UpdatedAt = Now();
        await db.SaveChangesAsync(cancellationToken);
        return ToResult(log);
    }

    private async Task<WhatsAppProviderSetting?> ResolveSettingAsync(HttpContext context, Guid storeId, CancellationToken cancellationToken)
        => await WorkspaceScope.ApplyTo(db.WhatsAppProviderSettings, context)
            .Where(item => item.StoreId == storeId && !item.Deleted)
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    private static WhatsAppProviderSettingSafeDto ToSafeDto(WhatsAppProviderSetting item)
        => new(
            item.Id,
            item.CompanyId,
            item.StoreGroupId,
            item.StoreId,
            item.IsEnabled,
            item.AutoSendDigitalBills,
            item.Provider,
            item.ApiBaseUrl,
            !string.IsNullOrWhiteSpace(item.ApiToken),
            item.PhoneNumberId,
            item.SenderId,
            item.TemplateName,
            item.LanguageCode,
            item.MessageTemplateText,
            item.SendPdfLink,
            item.FallbackToManualLog,
            item.RetryLimit,
            item.LastTestAt,
            item.LastError);

    private static WhatsAppSendResultDto ToResult(WhatsAppMessageLog log)
        => new(
            log.Id,
            log.DigitalInvoiceId,
            log.Status,
            log.Provider,
            log.CustomerMobile,
            log.MessageBody,
            log.ProviderMessageId,
            log.ErrorMessage,
            log.CreatedAt,
            log.SentAt);

    private static string DefaultTemplate()
        => "Hello {{customerName}}, thank you for shopping at Aadwika Fashion. Your invoice {{invoiceNumber}} of ₹{{amount}} is ready. View / download your bill: {{publicUrl}}. Team Aadwika Fashion, Dumka.";

    private static string RenderTemplate(string? template, DigitalInvoice invoice, string storeName, string publicUrl)
    {
        var body = string.IsNullOrWhiteSpace(template) ? DefaultTemplate() : template;
        return body
            .Replace("{{customerName}}", string.IsNullOrWhiteSpace(invoice.CustomerName) ? "Customer" : invoice.CustomerName, StringComparison.OrdinalIgnoreCase)
            .Replace("{{storeName}}", storeName, StringComparison.OrdinalIgnoreCase)
            .Replace("{{invoiceNumber}}", invoice.InvoiceNumber, StringComparison.OrdinalIgnoreCase)
            .Replace("{{amount}}", invoice.Amount.ToString("0.##"), StringComparison.OrdinalIgnoreCase)
            .Replace("{{publicUrl}}", publicUrl, StringComparison.OrdinalIgnoreCase)
            .Replace("{{publicPath}}", invoice.PublicUrl ?? $"/i/{invoice.PublicToken}", StringComparison.OrdinalIgnoreCase);
    }


    private static IEnumerable<(string MessageId, string Status, DateTime? EventAt, string? ErrorMessage)> ExtractMetaStatuses(JsonElement payload)
    {
        if (!payload.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var change in changes.EnumerateArray())
            {
                if (!change.TryGetProperty("value", out var value)
                    || !value.TryGetProperty("statuses", out var statuses)
                    || statuses.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var status in statuses.EnumerateArray())
                {
                    var id = status.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
                    var statusText = status.TryGetProperty("status", out var statusElement) ? statusElement.GetString() : null;
                    DateTime? eventAt = null;
                    if (status.TryGetProperty("timestamp", out var timestampElement))
                    {
                        var timestampText = timestampElement.ValueKind == JsonValueKind.Number
                            ? timestampElement.GetInt64().ToString()
                            : timestampElement.GetString();
                        if (long.TryParse(timestampText, out var seconds))
                        {
                            eventAt = DateTime.SpecifyKind(DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime, DateTimeKind.Unspecified);
                        }
                    }

                    string? errorMessage = null;
                    if (status.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
                    {
                        var first = errors[0];
                        var title = first.TryGetProperty("title", out var titleElement) ? titleElement.GetString() : null;
                        var message = first.TryGetProperty("message", out var messageElement) ? messageElement.GetString() : null;
                        var details = first.TryGetProperty("error_data", out var errorData) && errorData.TryGetProperty("details", out var detailsElement)
                            ? detailsElement.GetString()
                            : null;
                        errorMessage = Trim(string.Join(" - ", new[] { title, message, details }.Where(part => !string.IsNullOrWhiteSpace(part))), 900);
                    }

                    yield return (id ?? string.Empty, statusText ?? string.Empty, eventAt, errorMessage);
                }
            }
        }
    }

    private static string NormalizeWebhookStatus(string? status)
    {
        var normalized = Regex.Replace(status ?? string.Empty, @"[^A-Za-z]", string.Empty);
        return normalized.Equals("sent", StringComparison.OrdinalIgnoreCase) ? "Sent"
            : normalized.Equals("delivered", StringComparison.OrdinalIgnoreCase) ? "Delivered"
            : normalized.Equals("read", StringComparison.OrdinalIgnoreCase) ? "Read"
            : normalized.Equals("failed", StringComparison.OrdinalIgnoreCase) ? "Failed"
            : string.IsNullOrWhiteSpace(normalized) ? "WebhookReceived" : normalized;
    }

    private static string BuildDefaultMessage(DigitalInvoice invoice, string storeName, string publicUrl)
        => RenderTemplate(DefaultTemplate(), invoice, storeName, publicUrl);

    private string BuildPublicUrl(DigitalInvoice invoice, HttpContext context)
    {
        var configuredBase = configuration["DigitalBills:PublicBaseUrl"]?.Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(configuredBase))
        {
            return configuredBase + (invoice.PublicUrl ?? $"/i/{Uri.EscapeDataString(invoice.PublicToken)}");
        }

        var forwardedProto = context.Request.Headers["X-Forwarded-Proto"].FirstOrDefault();
        var forwardedHost = context.Request.Headers["X-Forwarded-Host"].FirstOrDefault();
        var scheme = string.IsNullOrWhiteSpace(forwardedProto) ? context.Request.Scheme : forwardedProto;
        var host = string.IsNullOrWhiteSpace(forwardedHost) ? context.Request.Host.Value : forwardedHost;
        return $"{scheme}://{host}{(invoice.PublicUrl ?? $"/i/{Uri.EscapeDataString(invoice.PublicToken)}")}";
    }

    private static string NormalizeProvider(string? provider)
    {
        var value = provider?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return ManualOnlyProvider;
        }

        return value.Equals(MetaCloudProvider, StringComparison.OrdinalIgnoreCase) ? MetaCloudProvider
            : value.Equals(ManualOnlyProvider, StringComparison.OrdinalIgnoreCase) ? ManualOnlyProvider
            : value;
    }

    private static string? NormalizeMobileForSend(string? mobile)
    {
        var digits = new string((mobile ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length == 10)
        {
            digits = "91" + digits;
        }

        return digits.Length is >= 11 and <= 15 ? digits : null;
    }

    private static string NormalizeMobileForStorage(string? mobile)
    {
        var digits = new string((mobile ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length > 20 ? digits[^20..] : digits;
    }

    private static string? CleanNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Trim(string value, int length) => value.Length <= length ? value : value[..length];

    private static string? ExtractMetaMessageId(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            if (root.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array && messages.GetArrayLength() > 0)
            {
                var first = messages[0];
                if (first.TryGetProperty("id", out var id))
                {
                    return id.GetString();
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string ResolveActor(HttpContext context)
        => context.User.Identity?.Name
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
            ?? context.User.FindFirst("userName")?.Value
            ?? "System";

    private static DateTime Now() => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
}

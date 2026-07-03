namespace Garmetix.Api.Marketing;

public sealed record DigitalBillListItemDto(
    Guid Id,
    Guid InvoiceId,
    string InvoiceType,
    string InvoiceNumber,
    DateTime InvoiceDate,
    string CustomerName,
    string CustomerMobile,
    decimal Amount,
    string PublicToken,
    string PublicPath,
    DateTime? ExpiresAt,
    bool IsActive,
    string WhatsAppStatus,
    int OpenCount,
    int PdfDownloadCount,
    int ReviewClickCount,
    int FeedbackCount,
    DateTime CreatedAt,
    DateTime? LastOpenedAt);

public sealed record DigitalBillListResponseDto(
    IReadOnlyList<DigitalBillListItemDto> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record DigitalBillActionResponseDto(
    Guid Id,
    Guid InvoiceId,
    string InvoiceNumber,
    string PublicToken,
    string PublicPath,
    DateTime? ExpiresAt,
    bool IsActive,
    string Message);

public sealed record DisableDigitalBillRequest(string? Reason);

public sealed record GenerateDigitalBillRequest(string? InvoiceId, string? InvoiceNumber, string? InvoiceKey);

public sealed record PublicDigitalInvoiceDto(
    Guid Id,
    string InvoiceNumber,
    DateTime InvoiceDate,
    string InvoiceStatus,
    string CompanyName,
    string CompanyAddress,
    string CompanyPhone,
    string CompanyGstin,
    string StoreName,
    string CustomerName,
    string CustomerMobileMasked,
    decimal Mrp,
    decimal DiscountAmount,
    decimal NetAmount,
    decimal TaxAmount,
    decimal RoundOff,
    decimal BillAmount,
    decimal PaidAmount,
    decimal BalanceAmount,
    string PublicToken,
    string PdfUrl,
    string GoodsReturnPolicyUrl,
    DateTime? ExpiresAt,
    PublicReviewSettingDto ReviewSettings,
    IReadOnlyList<PublicInvoiceItemDto> Items,
    IReadOnlyList<PublicAdBannerDto> Banners);

public sealed record PublicInvoiceItemDto(
    string ProductName,
    string Barcode,
    decimal Quantity,
    decimal Mrp,
    decimal DiscountAmount,
    decimal TaxPercentage,
    decimal TaxAmount,
    string? HsnCode,
    string? Unit,
    decimal Amount);

public sealed record PublicReviewSettingDto(
    string? GoogleReviewUrl,
    string? InstagramUrl,
    string? FacebookUrl,
    string? WhatsAppSupportNumber,
    bool EnableGoogleReview,
    bool EnableInstagram,
    bool EnableFacebook,
    bool EnableWhatsappSupport,
    bool EnablePrivateFeedback,
    string ReviewButtonText,
    string FeedbackButtonText);

public sealed record PublicAdBannerDto(
    Guid Id,
    string Title,
    string ImageUrl,
    string? TargetUrl,
    string Position);

public sealed record DigitalBillEventRequest(
    string EventType,
    string? Source = null,
    string? TargetUrl = null,
    string? DetailsJson = null);

public sealed record CustomerFeedbackSubmitRequest(
    int Rating,
    string? Message);

public sealed record StoreReviewSettingDto(
    Guid? Id,
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId,
    string? GoogleReviewUrl,
    string? InstagramUrl,
    string? FacebookUrl,
    string? WhatsAppSupportNumber,
    bool EnableGoogleReview,
    bool EnableInstagram,
    bool EnableFacebook,
    bool EnableWhatsappSupport,
    bool EnablePrivateFeedback,
    string? ReviewButtonText,
    string? FeedbackButtonText);


public sealed record DigitalBillEventDto(
    Guid Id,
    string EventType,
    string? Source,
    string? TargetUrl,
    DateTime EventAt,
    string? IpAddress,
    string? UserAgent);


public sealed record DigitalBillActivityDto(
    DigitalBillListItemDto Summary,
    DigitalBillActivityTotalsDto Totals,
    IReadOnlyList<DigitalBillTimelineItemDto> Timeline,
    IReadOnlyList<DigitalBillEventDto> Events,
    IReadOnlyList<WhatsAppMessageLogDto> WhatsAppLogs,
    IReadOnlyList<CustomerFeedbackDto> Feedback);

public sealed record DigitalBillActivityTotalsDto(
    int OpenCount,
    int PdfDownloadCount,
    int ReviewClickCount,
    int FeedbackCount,
    int BannerClickCount,
    int WhatsAppLogCount,
    int WhatsAppSentCount,
    int WhatsAppFailedCount);

public sealed record DigitalBillTimelineItemDto(
    DateTime At,
    string Type,
    string Title,
    string Description,
    string? Source,
    string? TargetUrl,
    string? Detail);

public sealed record CustomerFeedbackDto(
    Guid Id,
    Guid DigitalInvoiceId,
    string InvoiceNumber,
    string CustomerName,
    string CustomerMobile,
    int Rating,
    string? Message,
    DateTime SubmittedAt);

public sealed record WhatsAppProviderSettingDto(
    Guid? Id,
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId,
    bool IsEnabled,
    bool AutoSendDigitalBills,
    string Provider,
    string? ApiBaseUrl,
    string? ApiToken,
    string? PhoneNumberId,
    string? SenderId,
    string? TemplateName,
    string LanguageCode,
    string MessageTemplateText,
    bool SendPdfLink,
    bool FallbackToManualLog,
    int RetryLimit,
    DateTime? LastTestAt,
    string? LastError);

public sealed record WhatsAppProviderSettingSafeDto(
    Guid Id,
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId,
    bool IsEnabled,
    bool AutoSendDigitalBills,
    string Provider,
    string? ApiBaseUrl,
    bool HasApiToken,
    string? PhoneNumberId,
    string? SenderId,
    string? TemplateName,
    string LanguageCode,
    string MessageTemplateText,
    bool SendPdfLink,
    bool FallbackToManualLog,
    int RetryLimit,
    DateTime? LastTestAt,
    string? LastError);

public sealed record WhatsAppTestSendRequest(
    Guid StoreId,
    string CustomerMobile,
    string? CustomerName = null,
    string? Message = null);

public sealed record SendDigitalBillWhatsAppRequest(
    bool Force = false);

public sealed record WhatsAppSendResultDto(
    Guid LogId,
    Guid? DigitalInvoiceId,
    string Status,
    string Provider,
    string CustomerMobile,
    string MessageBody,
    string? ProviderMessageId,
    string? ErrorMessage,
    DateTime CreatedAt,
    DateTime? SentAt);

public sealed record WhatsAppMessageLogDto(
    Guid Id,
    Guid? DigitalInvoiceId,
    string? InvoiceNumber,
    string CustomerMobile,
    string Provider,
    string? TemplateName,
    string MessageBody,
    string Status,
    string? ProviderMessageId,
    string? ErrorMessage,
    int RetryCount,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? DeliveredAt,
    DateTime? ReadAt);

public sealed record WhatsAppMessageLogListResponseDto(
    IReadOnlyList<WhatsAppMessageLogDto> Items,
    int Total,
    int Page,
    int PageSize);



public sealed record InvoiceAdBannerDto(
    Guid Id,
    Guid CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string? StoreName,
    string Title,
    string ImageUrl,
    string? TargetUrl,
    string Position,
    DateTime? StartDate,
    DateTime? EndDate,
    bool IsActive,
    int Priority,
    int ClickCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record InvoiceAdBannerListResponseDto(
    IReadOnlyList<InvoiceAdBannerDto> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record InvoiceAdBannerUpsertRequest(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string Title,
    string ImageUrl,
    string? TargetUrl,
    string Position,
    DateTime? StartDate,
    DateTime? EndDate,
    bool IsActive = true,
    int Priority = 0);

public sealed record DigitalBillAnalyticsDto(
    DateTime FromDate,
    DateTime ToDate,
    int TotalDigitalBills,
    int ActiveDigitalBills,
    int OpenCount,
    int PdfDownloadCount,
    int ReviewClickCount,
    int FeedbackCount,
    int BannerClickCount,
    int WhatsAppSentCount,
    int WhatsAppDeliveredCount,
    int WhatsAppReadCount,
    int WhatsAppFailedCount,
    IReadOnlyList<DigitalBillAnalyticsDayDto> Daily);

public sealed record DigitalBillAnalyticsDayDto(
    DateTime Date,
    int Bills,
    int Opens,
    int PdfDownloads,
    int ReviewClicks,
    int Feedbacks,
    int BannerClicks,
    int WhatsAppSent,
    int WhatsAppDelivered,
    int WhatsAppRead,
    int WhatsAppFailed);

public sealed record WhatsAppWebhookResultDto(
    string Provider,
    int Updated,
    int Ignored,
    string Message);

public sealed record DigitalBillAudienceResponseDto(
    IReadOnlyList<DigitalBillAudienceItemDto> Items,
    IReadOnlyList<DigitalBillAudienceSummaryDto> Summaries,
    int Total,
    int Page,
    int PageSize,
    string Segment,
    DateTime FromDate,
    DateTime ToDate,
    bool IsTruncated,
    DateTime GeneratedAt);

public sealed record DigitalBillAudienceSummaryDto(
    string Segment,
    string Label,
    int Count,
    string Hint);

public sealed record DigitalBillAudienceItemDto(
    string AudienceKey,
    string CustomerName,
    string CustomerMobile,
    Guid? StoreId,
    string StoreName,
    Guid LastDigitalBillId,
    string LastInvoiceNumber,
    DateTime LastInvoiceDate,
    string LastPublicPath,
    int InvoiceCount,
    decimal TotalAmount,
    int OpenCount,
    int PdfDownloadCount,
    int ReviewClickCount,
    int FeedbackCount,
    int BannerClickCount,
    int WhatsAppSentCount,
    int WhatsAppFailedCount,
    string LastWhatsAppStatus,
    int? LastFeedbackRating,
    int? WorstFeedbackRating,
    DateTime? LastActivityAt,
    string Recommendation);


public sealed record DigitalBillCampaignListResponseDto(
    IReadOnlyList<DigitalBillCampaignDto> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record DigitalBillCampaignDto(
    Guid Id,
    Guid CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string? StoreName,
    string Name,
    string Segment,
    DateTime FromDate,
    DateTime ToDate,
    string? SearchText,
    string Channel,
    string Status,
    string? TemplateName,
    string MessageTitle,
    string MessageBody,
    string? OfferUrl,
    string? Notes,
    int RecipientCount,
    int PreparedCount,
    int SentCount,
    int FailedCount,
    int OpenCountAtCreate,
    int ReviewClickCountAtCreate,
    int FeedbackCountAtCreate,
    DateTime? ScheduledAt,
    DateTime? QueuedAt,
    DateTime? CompletedAt,
    DateTime? CancelledAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record DigitalBillCampaignRecipientDto(
    Guid Id,
    Guid CampaignId,
    Guid DigitalInvoiceId,
    string AudienceKey,
    string CustomerName,
    string CustomerMobile,
    Guid StoreId,
    string StoreName,
    string InvoiceNumber,
    string PublicPath,
    decimal LastInvoiceAmount,
    string Status,
    string MessageBody,
    string? ErrorMessage,
    DateTime? QueuedAt,
    DateTime? SentAt,
    DateTime? FailedAt,
    DateTime CreatedAt);

public sealed record DigitalBillCampaignPreviewRequest(
    string? Name,
    string? Segment,
    DateTime? FromDate,
    DateTime? ToDate,
    Guid? StoreId,
    string? SearchText,
    string? MessageTitle,
    string? MessageBody,
    string? OfferUrl,
    int Limit = 1000);

public sealed record DigitalBillCampaignCreateRequest(
    string Name,
    string? Segment,
    DateTime? FromDate,
    DateTime? ToDate,
    Guid? StoreId,
    string? SearchText,
    string? Channel,
    string? TemplateName,
    string? MessageTitle,
    string MessageBody,
    string? OfferUrl,
    string? Notes,
    DateTime? ScheduledAt,
    bool QueueNow = false,
    int Limit = 1000);

public sealed record DigitalBillCampaignPreviewDto(
    string Segment,
    DateTime FromDate,
    DateTime ToDate,
    Guid? StoreId,
    string? SearchText,
    int TotalCandidates,
    int RecipientsWithMobile,
    int SkippedNoMobile,
    int LimitApplied,
    IReadOnlyList<DigitalBillCampaignRecipientDto> SampleRecipients);

public sealed record DigitalBillCampaignDetailDto(
    DigitalBillCampaignDto Campaign,
    IReadOnlyList<DigitalBillCampaignRecipientDto> Recipients);

public sealed record DigitalBillCampaignStatusRequest(string? Reason = null);


public sealed record DigitalBillCampaignSendRequest(
    bool Force = false,
    int Limit = 500);

public sealed record DigitalBillCampaignSendResultDto(
    Guid CampaignId,
    string CampaignName,
    string Status,
    int Attempted,
    int Sent,
    int ManualPending,
    int Failed,
    int Skipped,
    string Message,
    IReadOnlyList<DigitalBillCampaignSendRecipientResultDto> Recipients);

public sealed record DigitalBillCampaignSendRecipientResultDto(
    Guid RecipientId,
    Guid? LogId,
    string CustomerName,
    string CustomerMobile,
    string Status,
    string? ProviderMessageId,
    string? ErrorMessage);

public sealed record DigitalBillProductionReadinessDto(
    string OverallStatus,
    DateTime GeneratedAt,
    string? PublicBaseUrl,
    bool HasWhatsAppWebhookVerifyToken,
    int DigitalBillCount,
    int ActiveDigitalBillCount,
    int WhatsAppSettingsCount,
    int EnabledWhatsAppSettingsCount,
    int MetaWhatsAppSettingsCount,
    int ReviewSettingsCount,
    int ActiveBannerCount,
    int CampaignCount,
    int FeedbackCount,
    IReadOnlyList<DigitalBillProductionCheckDto> Checks);

public sealed record DigitalBillProductionCheckDto(
    string Code,
    string Label,
    string Status,
    string Message);

public sealed record DigitalBillCampaignRoiDto(
    Guid CampaignId,
    string CampaignName,
    string Status,
    DateTime AttributionFrom,
    DateTime AttributionTo,
    int AttributionDays,
    int RecipientCount,
    int SentCount,
    int OpenedAfterCount,
    int PdfDownloadedAfterCount,
    int ReviewClickedAfterCount,
    int FeedbackSubmittedAfterCount,
    int BannerClickedAfterCount,
    int EngagedRecipientCount,
    int RepeatCustomerCount,
    int RepeatInvoiceCount,
    decimal RepeatSalesAmount,
    decimal AverageRepeatBillAmount,
    decimal EngagementRate,
    decimal RepeatPurchaseRate,
    IReadOnlyList<DigitalBillCampaignRecipientRoiDto> Recipients,
    IReadOnlyList<DigitalBillCampaignRepeatSaleDto> RepeatSales);

public sealed record DigitalBillCampaignRecipientRoiDto(
    Guid RecipientId,
    Guid DigitalInvoiceId,
    string CustomerName,
    string CustomerMobile,
    string InvoiceNumber,
    string PublicPath,
    string Status,
    DateTime? SentAt,
    int OpenedAfterCount,
    int PdfDownloadedAfterCount,
    int ReviewClickedAfterCount,
    int FeedbackSubmittedAfterCount,
    int BannerClickedAfterCount,
    int RepeatInvoiceCount,
    decimal RepeatSalesAmount,
    DateTime? LastRepeatPurchaseAt);

public sealed record DigitalBillCampaignRepeatSaleDto(
    Guid InvoiceId,
    string InvoiceNumber,
    DateTime InvoiceDate,
    string CustomerName,
    string CustomerMobile,
    Guid StoreId,
    string StoreName,
    decimal BillAmount);


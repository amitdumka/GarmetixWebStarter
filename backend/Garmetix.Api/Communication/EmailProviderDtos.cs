namespace Garmetix.Api.Communication;

public sealed record EmailProviderSummaryDto(
    Guid Id,
    string ProviderName,
    string ProviderType,
    string? SmtpPresetKey,
    bool IsEnabled,
    bool IsDefault,
    int Priority,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId);

public sealed record EmailProviderDetailDto(
    Guid Id,
    string ProviderName,
    string ProviderType,
    string? SmtpPresetKey,
    string? Host,
    int? Port,
    bool EnableSsl,
    bool UseStartTls,
    string FromEmail,
    string FromName,
    string? ReplyToEmail,
    bool IsEnabled,
    bool IsDefault,
    int Priority,
    int TimeoutSeconds,
    int MaxRetries,
    int? DailyRateLimit,
    int? PerMinuteRateLimit,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? CreatedBy,
    string? UpdatedBy);

public sealed record EmailProviderSaveRequest(
    string ProviderName,
    string ProviderType,
    string? SmtpPresetKey,
    string? Host,
    int? Port,
    bool EnableSsl,
    bool UseStartTls,
    string FromEmail,
    string FromName,
    string? ReplyToEmail,
    bool IsEnabled,
    bool IsDefault,
    int Priority,
    int TimeoutSeconds,
    int MaxRetries,
    int? DailyRateLimit,
    int? PerMinuteRateLimit,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string? Notes);

public sealed record EmailProviderCatalogDto(
    IReadOnlyList<string> ProviderTypes,
    IReadOnlyList<string> CredentialKeys,
    IReadOnlyDictionary<string, SmtpPresetDefaults> SmtpPresets);

public sealed record EmailCredentialEntryDto(string CredentialKey, bool HasValue, string? MaskedDisplayValue, DateTime? UpdatedAt);

public sealed record EmailCredentialSaveEntry(string CredentialKey, string? Value, bool Clear);

public sealed record EmailCredentialSaveRequest(List<EmailCredentialSaveEntry> Entries);

public sealed record EmailSendTestRequest(string ToEmail);

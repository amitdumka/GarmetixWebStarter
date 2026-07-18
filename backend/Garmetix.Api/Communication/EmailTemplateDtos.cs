namespace Garmetix.Api.Communication;

public sealed record EmailTemplateSummaryDto(
    Guid Id,
    string TemplateKey,
    string DisplayName,
    string? Category,
    bool IsSystemTemplate,
    bool IsActive,
    Guid? CurrentVersionId,
    int? CurrentVersionNumber,
    string? CurrentVersionStatus);

public sealed record EmailTemplateVersionSummaryDto(
    Guid Id,
    int VersionNumber,
    string Subject,
    string Status,
    DateTime CreatedAt,
    DateTime? ApprovedAtUtc);

public sealed record EmailTemplateDetailDto(
    Guid Id,
    string TemplateKey,
    string DisplayName,
    string? Category,
    bool IsSystemTemplate,
    bool IsActive,
    Guid? CurrentVersionId,
    IReadOnlyList<EmailTemplateVersionSummaryDto> Versions);

public sealed record EmailTemplateVersionDetailDto(
    Guid Id,
    Guid TemplateId,
    int VersionNumber,
    string Subject,
    string HtmlBody,
    string? TextBody,
    string? SampleDataJson,
    string Status,
    DateTime CreatedAt,
    DateTime? ApprovedAtUtc);

public sealed record EmailTemplateCreateRequest(string TemplateKey, string DisplayName, string? Category);

public sealed record EmailTemplateUpdateRequest(string DisplayName, string? Category, bool IsActive);

public sealed record EmailTemplateVersionCreateRequest(string Subject, string HtmlBody, string? TextBody, string? SampleDataJson);

public sealed record EmailTemplatePreviewRequest(string? SampleDataJsonOverride);

public sealed record EmailTemplatePreviewResultDto(string Subject, string HtmlBody, string? TextBody);

public sealed record EmailTemplateTestSendRequest(string ToEmail, string? SampleDataJsonOverride);

using Garmetix.Api.Auth;
using Garmetix.Core.Models.Communication;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Communication;

/// <summary>
/// Template CRUD, versioning/approval/restore, safe preview and test-send. Read (list/detail/
/// preview) sits on the base Communication policy so anyone composing a broadcast can see what
/// a template will look like; every mutation (create/edit/version/approve/restore/test-send)
/// requires CommunicationTemplates (Admin/PowerUser tier).
/// </summary>
public static class EmailTemplateEndpoints
{
    public static RouteGroupBuilder MapEmailTemplateEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/communication/templates")
            .WithTags("Communication & Mail - Templates")
            .RequireAuthorization(GarmetixPolicies.Communication);

        group.MapGet("/", GetTemplatesAsync);
        group.MapGet("/{id:guid}", GetTemplateAsync);
        group.MapPost("/", CreateTemplateAsync).RequireAuthorization(GarmetixPolicies.CommunicationTemplates);
        group.MapPut("/{id:guid}", UpdateTemplateAsync).RequireAuthorization(GarmetixPolicies.CommunicationTemplates);

        group.MapGet("/{id:guid}/versions/{versionId:guid}", GetVersionAsync);
        group.MapPost("/{id:guid}/versions", CreateVersionAsync).RequireAuthorization(GarmetixPolicies.CommunicationTemplates);
        group.MapPost("/{id:guid}/versions/{versionId:guid}/approve", ApproveVersionAsync).RequireAuthorization(GarmetixPolicies.CommunicationTemplates);
        group.MapPost("/{id:guid}/versions/{versionId:guid}/restore", RestoreVersionAsync).RequireAuthorization(GarmetixPolicies.CommunicationTemplates);

        group.MapPost("/{id:guid}/preview", PreviewTemplateAsync);
        group.MapPost("/{id:guid}/test-send", TestSendTemplateAsync).RequireAuthorization(GarmetixPolicies.CommunicationTemplates);

        return group;
    }

    private static async Task<IResult> GetTemplatesAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var templates = await db.EmailTemplates.AsNoTracking().OrderBy(t => t.DisplayName).ToListAsync(cancellationToken);
        var currentVersionIds = templates.Where(t => t.CurrentVersionId != null).Select(t => t.CurrentVersionId!.Value).ToArray();
        var currentVersions = await db.EmailTemplateVersions.AsNoTracking()
            .Where(v => currentVersionIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        var result = templates.Select(t =>
        {
            currentVersions.TryGetValue(t.CurrentVersionId ?? Guid.Empty, out var version);
            return new EmailTemplateSummaryDto(t.Id, t.TemplateKey, t.DisplayName, t.Category, t.IsSystemTemplate, t.IsActive, t.CurrentVersionId, version?.VersionNumber, version?.Status);
        });

        return Results.Ok(result);
    }

    private static async Task<IResult> GetTemplateAsync(Guid id, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var template = await db.EmailTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (template is null)
        {
            return Results.NotFound(new { message = "Template not found." });
        }

        var versions = await db.EmailTemplateVersions.AsNoTracking()
            .Where(v => v.TemplateId == id)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new EmailTemplateVersionSummaryDto(v.Id, v.VersionNumber, v.Subject, v.Status, v.CreatedAt, v.ApprovedAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(new EmailTemplateDetailDto(template.Id, template.TemplateKey, template.DisplayName, template.Category, template.IsSystemTemplate, template.IsActive, template.CurrentVersionId, versions));
    }

    private static async Task<IResult> CreateTemplateAsync(EmailTemplateCreateRequest request, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateKey) || string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return Results.BadRequest(new { message = "TemplateKey and DisplayName are required." });
        }

        var keyExists = await db.EmailTemplates.AsNoTracking().AnyAsync(t => t.CompanyId == null && t.TemplateKey == request.TemplateKey, cancellationToken);
        if (keyExists)
        {
            return Results.BadRequest(new { message = $"A template with key '{request.TemplateKey}' already exists." });
        }

        var template = new EmailTemplate
        {
            TemplateKey = request.TemplateKey.Trim(),
            DisplayName = request.DisplayName.Trim(),
            Category = request.Category,
            IsSystemTemplate = false,
            IsActive = true,
        };
        db.EmailTemplates.Add(template);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new EmailTemplateDetailDto(template.Id, template.TemplateKey, template.DisplayName, template.Category, template.IsSystemTemplate, template.IsActive, null, []));
    }

    private static async Task<IResult> UpdateTemplateAsync(Guid id, EmailTemplateUpdateRequest request, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var template = await db.EmailTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (template is null)
        {
            return Results.NotFound(new { message = "Template not found." });
        }

        template.DisplayName = request.DisplayName.Trim();
        template.Category = request.Category;
        template.IsActive = request.IsActive;
        template.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { message = "Template updated." });
    }

    private static async Task<IResult> GetVersionAsync(Guid id, Guid versionId, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var version = await db.EmailTemplateVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == versionId && v.TemplateId == id, cancellationToken);
        return version is null
            ? Results.NotFound(new { message = "Template version not found." })
            : Results.Ok(ToVersionDetailDto(version));
    }

    private static async Task<IResult> CreateVersionAsync(Guid id, EmailTemplateVersionCreateRequest request, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var template = await db.EmailTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (template is null)
        {
            return Results.NotFound(new { message = "Template not found." });
        }

        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.HtmlBody))
        {
            return Results.BadRequest(new { message = "Subject and HtmlBody are required." });
        }

        var nextVersionNumber = 1 + await db.EmailTemplateVersions.Where(v => v.TemplateId == id).Select(v => v.VersionNumber).DefaultIfEmpty(0).MaxAsync(cancellationToken);
        var version = new EmailTemplateVersion
        {
            TemplateId = id,
            VersionNumber = nextVersionNumber,
            Subject = request.Subject,
            HtmlBody = request.HtmlBody,
            TextBody = request.TextBody,
            SampleDataJson = request.SampleDataJson,
            Status = EmailCatalog.TemplateVersionStatuses.Draft,
            CreatedByUserId = ParseUserId(context),
        };
        db.EmailTemplateVersions.Add(version);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToVersionDetailDto(version));
    }

    private static async Task<IResult> ApproveVersionAsync(Guid id, Guid versionId, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var version = await db.EmailTemplateVersions.FirstOrDefaultAsync(v => v.Id == versionId && v.TemplateId == id, cancellationToken);
        if (version is null)
        {
            return Results.NotFound(new { message = "Template version not found." });
        }

        version.Status = EmailCatalog.TemplateVersionStatuses.Approved;
        version.ApprovedByUserId = ParseUserId(context);
        version.ApprovedAtUtc = DateTime.UtcNow;
        version.UpdatedAt = DateTime.UtcNow;

        var template = await db.EmailTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (template is not null)
        {
            template.CurrentVersionId = version.Id;
            template.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { message = "Version approved and published as current." });
    }

    private static async Task<IResult> RestoreVersionAsync(Guid id, Guid versionId, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var version = await db.EmailTemplateVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == versionId && v.TemplateId == id, cancellationToken);
        if (version is null)
        {
            return Results.NotFound(new { message = "Template version not found." });
        }

        if (version.Status != EmailCatalog.TemplateVersionStatuses.Approved)
        {
            return Results.BadRequest(new { message = "Only a previously approved version can be restored as current." });
        }

        var template = await db.EmailTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (template is null)
        {
            return Results.NotFound(new { message = "Template not found." });
        }

        template.CurrentVersionId = version.Id;
        template.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { message = $"Version {version.VersionNumber} restored as current." });
    }

    private static async Task<IResult> PreviewTemplateAsync(Guid id, EmailTemplatePreviewRequest request, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var template = await db.EmailTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (template?.CurrentVersionId is null)
        {
            return Results.BadRequest(new { message = "Template has no published version to preview." });
        }

        var version = await db.EmailTemplateVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == template.CurrentVersionId, cancellationToken);
        if (version is null)
        {
            return Results.BadRequest(new { message = "Current version could not be loaded." });
        }

        var tokens = EmailTemplateRenderer.ParseSampleData(request.SampleDataJsonOverride ?? version.SampleDataJson);
        var rendered = EmailTemplateRenderer.Render(version.Subject, version.HtmlBody, version.TextBody, tokens);
        return Results.Ok(new EmailTemplatePreviewResultDto(rendered.Subject, rendered.HtmlBody, rendered.TextBody));
    }

    private static async Task<IResult> TestSendTemplateAsync(
        Guid id,
        EmailTemplateTestSendRequest request,
        GarmetixDbContext db,
        EmailProviderResolutionService providerResolution,
        IEmailProviderClientFactory clientFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ToEmail))
        {
            return Results.BadRequest(new { message = "A recipient email address is required." });
        }

        var template = await db.EmailTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (template?.CurrentVersionId is null)
        {
            return Results.BadRequest(new { message = "Template has no published version to send." });
        }

        var version = await db.EmailTemplateVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == template.CurrentVersionId, cancellationToken);
        if (version is null)
        {
            return Results.BadRequest(new { message = "Current version could not be loaded." });
        }

        var tokens = EmailTemplateRenderer.ParseSampleData(request.SampleDataJsonOverride ?? version.SampleDataJson);
        var rendered = EmailTemplateRenderer.Render(version.Subject, version.HtmlBody, version.TextBody, tokens);

        var companyId = WorkspaceIdOrNull(context, "companyId");
        var storeGroupId = WorkspaceIdOrNull(context, "storeGroupId");
        var storeId = WorkspaceIdOrNull(context, "storeId");
        var resolved = await providerResolution.ResolveAsync(companyId, storeGroupId, storeId, cancellationToken);
        var client = clientFactory.GetClient(resolved.Provider.ProviderType);

        var sendRequest = new EmailSendRequest(
            From: new EmailAddressValue(resolved.Provider.FromEmail, resolved.Provider.FromName),
            To: [new EmailAddressValue(request.ToEmail.Trim(), null)],
            Subject: $"[Template Test] {rendered.Subject}",
            HtmlBody: rendered.HtmlBody,
            TextBody: rendered.TextBody,
            ReplyToEmail: resolved.Provider.ReplyToEmail);

        var result = await client.SendAsync(resolved.Provider, resolved.Credentials, sendRequest, cancellationToken);
        return Results.Ok(new { result.IsSuccess, result.ProviderMessageId, result.ErrorCode, result.ErrorMessage });
    }

    private static Guid? ParseUserId(HttpContext context) =>
        Guid.TryParse(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    private static Guid? WorkspaceIdOrNull(HttpContext context, string claimType) =>
        Guid.TryParse(context.User.FindFirst(claimType)?.Value, out var id) ? id : null;

    private static EmailTemplateVersionDetailDto ToVersionDetailDto(EmailTemplateVersion version) => new(
        version.Id, version.TemplateId, version.VersionNumber, version.Subject, version.HtmlBody, version.TextBody,
        version.SampleDataJson, version.Status, version.CreatedAt, version.ApprovedAtUtc);
}

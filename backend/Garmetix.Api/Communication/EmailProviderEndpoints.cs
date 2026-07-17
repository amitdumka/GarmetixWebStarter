using Garmetix.Api.Auth;
using Garmetix.Core.Models.Communication;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Communication;

/// <summary>
/// Provider/credential admin surface for the Communication & Mail module - CRUD, masked
/// credential retain-or-rotate semantics, enable/disable/default, connection test, and send
/// test email. Mirrors GstTaxEndpoints.cs's provider section (Stage GST-2) exactly, since it
/// is the established precedent in this repo for "admin manages a scoped multi-provider
/// registry with encrypted credentials." All mutation routes require CommunicationProviders
/// (Admin-tier only) - read-only provider listing is available at the base Communication policy
/// so any user composing a broadcast can at least see which provider will be used, without
/// being able to see or change credentials.
/// </summary>
public static class EmailProviderEndpoints
{
    public static RouteGroupBuilder MapEmailProviderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/communication/providers")
            .WithTags("Communication & Mail - Providers")
            .RequireAuthorization(GarmetixPolicies.Communication);

        group.MapGet("/", GetProvidersAsync);
        group.MapGet("/catalog", GetCatalog);
        group.MapGet("/{id:guid}", GetProviderAsync);
        group.MapPost("/", CreateProviderAsync).RequireAuthorization(GarmetixPolicies.CommunicationProviders);
        group.MapPut("/{id:guid}", UpdateProviderAsync).RequireAuthorization(GarmetixPolicies.CommunicationProviders);
        group.MapDelete("/{id:guid}", DeleteProviderAsync).RequireAuthorization(GarmetixPolicies.CommunicationProviders);
        group.MapPost("/{id:guid}/enable", (Guid id, GarmetixDbContext db, CancellationToken ct) => SetEnabledAsync(id, true, db, ct))
            .RequireAuthorization(GarmetixPolicies.CommunicationProviders);
        group.MapPost("/{id:guid}/disable", (Guid id, GarmetixDbContext db, CancellationToken ct) => SetEnabledAsync(id, false, db, ct))
            .RequireAuthorization(GarmetixPolicies.CommunicationProviders);
        group.MapPost("/{id:guid}/set-default", SetDefaultAsync).RequireAuthorization(GarmetixPolicies.CommunicationProviders);

        group.MapGet("/{id:guid}/credentials", GetCredentialsAsync).RequireAuthorization(GarmetixPolicies.CommunicationProviders);
        group.MapPut("/{id:guid}/credentials", SaveCredentialsAsync).RequireAuthorization(GarmetixPolicies.CommunicationProviders);

        group.MapPost("/{id:guid}/test-connection", TestConnectionAsync).RequireAuthorization(GarmetixPolicies.CommunicationProviders);
        group.MapPost("/{id:guid}/send-test", SendTestEmailAsync).RequireAuthorization(GarmetixPolicies.CommunicationProviders);

        return group;
    }

    private static async Task<IResult> GetProvidersAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var providers = await db.EmailProviderConfigurations.AsNoTracking()
            .OrderBy(p => p.Priority)
            .ToListAsync(cancellationToken);

        return Results.Ok(providers.Select(ToSummaryDto));
    }

    private static IResult GetCatalog() => Results.Ok(new EmailProviderCatalogDto(
        [EmailCatalog.ProviderTypes.BrevoApi, EmailCatalog.ProviderTypes.Smtp, EmailCatalog.ProviderTypes.LocalMasterOnly],
        EmailCatalog.CredentialKeys,
        SmtpPresetCatalog.Presets));

    private static async Task<IResult> GetProviderAsync(Guid id, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var provider = await db.EmailProviderConfigurations.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return provider is null ? Results.NotFound(new { message = "Email provider not found." }) : Results.Ok(ToDetailDto(provider));
    }

    private static async Task<IResult> CreateProviderAsync(EmailProviderSaveRequest request, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var validation = ValidateSaveRequest(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { message = validation });
        }

        if (request.IsDefault)
        {
            await ClearExistingDefaultAsync(db, request.CompanyId, request.StoreGroupId, request.StoreId, cancellationToken);
        }

        var provider = new EmailProviderConfiguration
        {
            ProviderName = request.ProviderName.Trim(),
            ProviderType = request.ProviderType,
            SmtpPresetKey = request.SmtpPresetKey,
            Host = string.IsNullOrWhiteSpace(request.Host) ? null : request.Host.Trim(),
            Port = request.Port,
            EnableSsl = request.EnableSsl,
            UseStartTls = request.UseStartTls,
            FromEmail = request.FromEmail.Trim(),
            FromName = request.FromName.Trim(),
            ReplyToEmail = string.IsNullOrWhiteSpace(request.ReplyToEmail) ? null : request.ReplyToEmail.Trim(),
            IsEnabled = request.IsEnabled,
            IsDefault = request.IsDefault,
            Priority = request.Priority,
            TimeoutSeconds = Math.Clamp(request.TimeoutSeconds, 5, 120),
            MaxRetries = Math.Clamp(request.MaxRetries, 0, 10),
            DailyRateLimit = request.DailyRateLimit,
            PerMinuteRateLimit = request.PerMinuteRateLimit,
            CompanyId = request.CompanyId,
            StoreGroupId = request.StoreGroupId,
            StoreId = request.StoreId,
            Notes = request.Notes,
            CreatedBy = context.User.Identity?.Name,
        };

        db.EmailProviderConfigurations.Add(provider);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDetailDto(provider));
    }

    private static async Task<IResult> UpdateProviderAsync(Guid id, EmailProviderSaveRequest request, GarmetixDbContext db, HttpContext context, CancellationToken cancellationToken)
    {
        var validation = ValidateSaveRequest(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { message = validation });
        }

        var provider = await db.EmailProviderConfigurations.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (provider is null)
        {
            return Results.NotFound(new { message = "Email provider not found." });
        }

        if (request.IsDefault && !provider.IsDefault)
        {
            await ClearExistingDefaultAsync(db, request.CompanyId, request.StoreGroupId, request.StoreId, cancellationToken);
        }

        provider.ProviderName = request.ProviderName.Trim();
        provider.ProviderType = request.ProviderType;
        provider.SmtpPresetKey = request.SmtpPresetKey;
        provider.Host = string.IsNullOrWhiteSpace(request.Host) ? null : request.Host.Trim();
        provider.Port = request.Port;
        provider.EnableSsl = request.EnableSsl;
        provider.UseStartTls = request.UseStartTls;
        provider.FromEmail = request.FromEmail.Trim();
        provider.FromName = request.FromName.Trim();
        provider.ReplyToEmail = string.IsNullOrWhiteSpace(request.ReplyToEmail) ? null : request.ReplyToEmail.Trim();
        provider.IsEnabled = request.IsEnabled;
        provider.IsDefault = request.IsDefault;
        provider.Priority = request.Priority;
        provider.TimeoutSeconds = Math.Clamp(request.TimeoutSeconds, 5, 120);
        provider.MaxRetries = Math.Clamp(request.MaxRetries, 0, 10);
        provider.DailyRateLimit = request.DailyRateLimit;
        provider.PerMinuteRateLimit = request.PerMinuteRateLimit;
        provider.CompanyId = request.CompanyId;
        provider.StoreGroupId = request.StoreGroupId;
        provider.StoreId = request.StoreId;
        provider.Notes = request.Notes;
        provider.UpdatedBy = context.User.Identity?.Name;
        provider.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDetailDto(provider));
    }

    private static async Task<IResult> DeleteProviderAsync(Guid id, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var provider = await db.EmailProviderConfigurations.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (provider is null)
        {
            return Results.NotFound(new { message = "Email provider not found." });
        }

        var credentials = await db.EmailProviderCredentials.Where(c => c.ProviderId == id).ToListAsync(cancellationToken);
        db.EmailProviderCredentials.RemoveRange(credentials);
        db.EmailProviderConfigurations.Remove(provider);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { message = "Email provider deleted." });
    }

    private static async Task<IResult> SetEnabledAsync(Guid id, bool enabled, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var provider = await db.EmailProviderConfigurations.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (provider is null)
        {
            return Results.NotFound(new { message = "Email provider not found." });
        }

        provider.IsEnabled = enabled;
        provider.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { message = enabled ? "Provider enabled." : "Provider disabled." });
    }

    private static async Task<IResult> SetDefaultAsync(Guid id, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var provider = await db.EmailProviderConfigurations.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (provider is null)
        {
            return Results.NotFound(new { message = "Email provider not found." });
        }

        await ClearExistingDefaultAsync(db, provider.CompanyId, provider.StoreGroupId, provider.StoreId, cancellationToken);
        provider.IsDefault = true;
        provider.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { message = "Provider set as default for this scope." });
    }

    private static async Task<IResult> GetCredentialsAsync(Guid id, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var providerExists = await db.EmailProviderConfigurations.AsNoTracking().AnyAsync(p => p.Id == id, cancellationToken);
        if (!providerExists)
        {
            return Results.NotFound(new { message = "Email provider not found." });
        }

        var stored = await db.EmailProviderCredentials.AsNoTracking()
            .Where(c => c.ProviderId == id)
            .ToDictionaryAsync(c => c.CredentialKey, c => c, cancellationToken);

        var result = EmailCatalog.CredentialKeys.Select(key => stored.TryGetValue(key, out var entry)
            ? new EmailCredentialEntryDto(key, true, entry.MaskedDisplayValue, entry.UpdatedAt)
            : new EmailCredentialEntryDto(key, false, null, null));

        return Results.Ok(result);
    }

    private static async Task<IResult> SaveCredentialsAsync(
        Guid id,
        EmailCredentialSaveRequest request,
        GarmetixDbContext db,
        EmailCredentialProtector protector,
        CancellationToken cancellationToken)
    {
        var providerExists = await db.EmailProviderConfigurations.AsNoTracking().AnyAsync(p => p.Id == id, cancellationToken);
        if (!providerExists)
        {
            return Results.NotFound(new { message = "Email provider not found." });
        }

        foreach (var entry in request.Entries)
        {
            if (!EmailCatalog.CredentialKeys.Contains(entry.CredentialKey))
            {
                continue;
            }

            var existing = await db.EmailProviderCredentials
                .FirstOrDefaultAsync(c => c.ProviderId == id && c.CredentialKey == entry.CredentialKey, cancellationToken);

            if (entry.Clear)
            {
                if (existing is not null)
                {
                    db.EmailProviderCredentials.Remove(existing);
                }

                continue;
            }

            if (string.IsNullOrEmpty(entry.Value))
            {
                // No value submitted and not clearing - leave the stored secret untouched (never re-encrypt a blank, this is the "retain" half of retain-or-rotate).
                continue;
            }

            var encrypted = protector.Protect(entry.Value);
            var masked = EmailCredentialProtector.Mask(entry.Value);

            if (existing is null)
            {
                db.EmailProviderCredentials.Add(new EmailProviderCredential
                {
                    ProviderId = id,
                    CredentialKey = entry.CredentialKey,
                    EncryptedValue = encrypted,
                    MaskedDisplayValue = masked,
                });
            }
            else
            {
                existing.EncryptedValue = encrypted;
                existing.MaskedDisplayValue = masked;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { message = "Credentials saved." });
    }

    private static async Task<IResult> TestConnectionAsync(Guid id, EmailProviderTestService testService, CancellationToken cancellationToken)
    {
        var result = await testService.TestConnectionAsync(id, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> SendTestEmailAsync(Guid id, EmailSendTestRequest request, EmailProviderTestService testService, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ToEmail))
        {
            return Results.BadRequest(new { message = "A recipient email address is required." });
        }

        var result = await testService.SendTestEmailAsync(id, request.ToEmail.Trim(), cancellationToken);
        return Results.Ok(new { result.IsSuccess, result.ProviderMessageId, result.ErrorCode, result.ErrorMessage });
    }

    private static async Task ClearExistingDefaultAsync(GarmetixDbContext db, Guid? companyId, Guid? storeGroupId, Guid? storeId, CancellationToken cancellationToken)
    {
        var existingDefaults = await db.EmailProviderConfigurations
            .Where(p => p.IsDefault && p.CompanyId == companyId && p.StoreGroupId == storeGroupId && p.StoreId == storeId)
            .ToListAsync(cancellationToken);
        foreach (var existing in existingDefaults)
        {
            existing.IsDefault = false;
        }
    }

    private static string? ValidateSaveRequest(EmailProviderSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProviderName))
        {
            return "Provider name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.FromEmail))
        {
            return "From email is required.";
        }

        if (request.ProviderType == EmailCatalog.ProviderTypes.Smtp && (string.IsNullOrWhiteSpace(request.Host) || request.Port is null))
        {
            return "Host and Port are required for an SMTP provider.";
        }

        return null;
    }

    private static EmailProviderSummaryDto ToSummaryDto(EmailProviderConfiguration provider) => new(
        provider.Id, provider.ProviderName, provider.ProviderType, provider.SmtpPresetKey,
        provider.IsEnabled, provider.IsDefault, provider.Priority,
        provider.CompanyId, provider.StoreGroupId, provider.StoreId);

    private static EmailProviderDetailDto ToDetailDto(EmailProviderConfiguration provider) => new(
        provider.Id, provider.ProviderName, provider.ProviderType, provider.SmtpPresetKey,
        provider.Host, provider.Port, provider.EnableSsl, provider.UseStartTls,
        provider.FromEmail, provider.FromName, provider.ReplyToEmail,
        provider.IsEnabled, provider.IsDefault, provider.Priority,
        provider.TimeoutSeconds, provider.MaxRetries, provider.DailyRateLimit, provider.PerMinuteRateLimit,
        provider.CompanyId, provider.StoreGroupId, provider.StoreId, provider.Notes,
        provider.CreatedAt, provider.UpdatedAt, provider.CreatedBy, provider.UpdatedBy);
}

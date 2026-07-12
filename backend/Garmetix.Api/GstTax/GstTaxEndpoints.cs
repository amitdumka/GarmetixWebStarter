using System.Diagnostics;
using System.Text.RegularExpressions;
using Garmetix.Api.Auth;
using Garmetix.Api.Database;
using Garmetix.Api.Workspace;
using Garmetix.Core.Models.GstTax;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.GstTax;

public static class GstTaxEndpoints
{
    private static readonly Regex GstinFormatPattern = new(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static RouteGroupBuilder MapGstTaxEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/gst")
            .WithTags("GST & Taxes")
            .RequireAuthorization(GarmetixPolicies.Gst);

        group.MapGet("/dashboard", GetDashboardAsync);

        group.MapGet("/providers", GetProvidersAsync);
        group.MapGet("/providers/catalog", GetProviderCatalog);
        group.MapGet("/providers/{id:guid}", GetProviderAsync);
        group.MapPost("/providers", CreateProviderAsync).RequireAuthorization(GarmetixPolicies.Admin);
        group.MapPut("/providers/{id:guid}", UpdateProviderAsync).RequireAuthorization(GarmetixPolicies.Admin);
        group.MapDelete("/providers/{id:guid}", DeleteProviderAsync).RequireAuthorization(GarmetixPolicies.Admin);
        group.MapPost("/providers/{id:guid}/enable", (Guid id, GarmetixDbContext db, ILoggerFactory lf, CancellationToken ct) => SetEnabledAsync(id, true, db, lf, ct)).RequireAuthorization(GarmetixPolicies.Admin);
        group.MapPost("/providers/{id:guid}/disable", (Guid id, GarmetixDbContext db, ILoggerFactory lf, CancellationToken ct) => SetEnabledAsync(id, false, db, lf, ct)).RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/providers/{id:guid}/credentials", GetCredentialsAsync).RequireAuthorization(GarmetixPolicies.Admin);
        group.MapPut("/providers/{id:guid}/credentials", SaveCredentialsAsync).RequireAuthorization(GarmetixPolicies.Admin);

        group.MapPost("/providers/{id:guid}/test-health", TestHealthAsync).RequireAuthorization(GarmetixPolicies.Admin);
        group.MapPost("/providers/{id:guid}/test-gstin", TestGstinAsync).RequireAuthorization(GarmetixPolicies.Admin);
        group.MapPost("/providers/{id:guid}/test-hsn", TestHsnAsync).RequireAuthorization(GarmetixPolicies.Admin);

        return group;
    }

    public sealed record GstinTestRequest(string? Gstin);
    public sealed record HsnTestRequest(string? HsnCode);

    private static async Task EnsureStorageAsync(GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await DatabaseSchemaRepairService.RepairGstTaxStorageAsync(
            db,
            loggerFactory.CreateLogger("GstTaxStorageRepair"),
            cancellationToken);
    }

    private static async Task<IResult> GetDashboardAsync(
        HttpContext context,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var products = WorkspaceScope.ApplyTo(db.Products.AsNoTracking().Where(p => !p.Deleted), context);
        var customers = WorkspaceScope.ApplyTo(db.Customers.AsNoTracking().Where(c => !c.Deleted), context);
        var vendors = WorkspaceScope.ApplyTo(db.Vendors.AsNoTracking().Where(v => !v.Deleted), context);
        var findings = WorkspaceScope.ApplyTo(db.GstAuditFindings.AsNoTracking().Where(f => !f.Deleted), context);

        var productsMissingHsn = await products.CountAsync(p => p.HSNCode == null || p.HSNCode == "", cancellationToken);
        var productsMissingGstRate = await products.CountAsync(p => p.TaxRate <= 0, cancellationToken);

        var verifiedCustomers = await customers.CountAsync(c => c.GSTVerified, cancellationToken);
        var verifiedVendors = await vendors.CountAsync(v => v.GSTVerified, cancellationToken);

        var inactiveCustomers = await customers.CountAsync(c =>
            c.GSTVerified && c.GSTRegistrationStatus != null && !c.GSTRegistrationStatus.ToLower().Contains("active"), cancellationToken);
        var inactiveVendors = await vendors.CountAsync(v =>
            v.GSTVerified && v.GSTRegistrationStatus != null && !v.GSTRegistrationStatus.ToLower().Contains("active"), cancellationToken);

        var openFindings = await findings.CountAsync(f => f.Status == "Open", cancellationToken);

        var providers = db.GstApiProviders.AsNoTracking();
        var totalProviders = await providers.CountAsync(cancellationToken);
        var enabledProviders = await providers.CountAsync(p => p.IsEnabled, cancellationToken);
        var localMaster = await providers.Where(p => p.ProviderType == "LocalMasterOnly").Select(p => p.ProviderName).FirstOrDefaultAsync(cancellationToken);

        var dashboard = new GstDashboardDto(
            GstinVerifiedCount: verifiedCustomers + verifiedVendors,
            InactiveGstinCount: inactiveCustomers + inactiveVendors,
            ProductsMissingHsnCount: productsMissingHsn,
            ProductsMissingGstRateCount: productsMissingGstRate,
            OpenGstAuditFindingsCount: openFindings,
            SaleGstMismatchCount: 0,
            PurchaseGstMismatchCount: 0,
            CurrentMonthTaxableSales: 0m,
            CurrentMonthOutputGst: 0m,
            CurrentMonthTaxablePurchases: 0m,
            CurrentMonthInputGst: 0m,
            EInvoicePendingCount: 0,
            EWayBillPendingCount: 0,
            ProviderHealth: new GstProviderHealthSummaryDto(totalProviders, enabledProviders, localMaster),
            PendingStageNotes:
            [
                "Sale/Purchase GST mismatch counts, monthly taxable/output/input GST totals, and E-Invoice/E-Way Bill pending counts are wired up in later GST & Taxes stages (Sale/Purchase Review, Returns, E-Invoice/E-Way Bill placeholders)."
            ]);

        return Results.Ok(dashboard);
    }

    private static IResult GetProviderCatalog() => Results.Ok(new GstProviderCatalogDto(
        GstTaxCatalog.ProviderTypes,
        GstTaxCatalog.Environments,
        GstTaxCatalog.FeatureCodes,
        GstTaxCatalog.CredentialKeys));

    private static async Task<IResult> GetProvidersAsync(
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var providers = await db.GstApiProviders.AsNoTracking()
            .OrderBy(p => p.Priority)
            .ToListAsync(cancellationToken);
        var featuresByProvider = await LoadFeaturesAsync(db, providers.Select(p => p.Id).ToArray(), cancellationToken);

        var result = providers.Select(provider => new GstProviderSummaryDto(
            provider.Id,
            provider.ProviderName,
            provider.ProviderType,
            provider.Environment,
            provider.IsEnabled,
            provider.Priority,
            provider.FallbackEnabled,
            featuresByProvider.Where(f => f.ProviderId == provider.Id).Select(f => f.FeatureCode).OrderBy(code => code).ToArray()));

        return Results.Ok(result);
    }

    private static async Task<IResult> GetProviderAsync(
        Guid id,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var provider = await db.GstApiProviders.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (provider is null)
        {
            return Results.NotFound(new { message = "GST API provider not found." });
        }

        var features = await LoadFeaturesAsync(db, [id], cancellationToken);
        return Results.Ok(ToDetailDto(provider, features));
    }

    private static async Task<IResult> CreateProviderAsync(
        GstProviderSaveRequest request,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var validation = ValidateSaveRequest(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { message = validation });
        }

        var provider = new GstApiProvider
        {
            ProviderName = request.ProviderName.Trim(),
            ProviderType = request.ProviderType,
            Environment = request.Environment,
            BaseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? null : request.BaseUrl.Trim(),
            AuthUrl = string.IsNullOrWhiteSpace(request.AuthUrl) ? null : request.AuthUrl.Trim(),
            IsEnabled = request.IsEnabled,
            Priority = request.Priority,
            FallbackEnabled = request.FallbackEnabled,
            TimeoutSeconds = Math.Clamp(request.TimeoutSeconds, 5, 120),
            MaxRetries = Math.Clamp(request.MaxRetries, 0, 5),
            Notes = request.Notes,
            CreatedBy = context.User.Identity?.Name
        };

        db.GstApiProviders.Add(provider);
        await db.SaveChangesAsync(cancellationToken);

        await SaveFeaturesAsync(db, provider.Id, request.Features, cancellationToken);

        var features = await LoadFeaturesAsync(db, [provider.Id], cancellationToken);
        return Results.Ok(ToDetailDto(provider, features));
    }

    private static async Task<IResult> UpdateProviderAsync(
        Guid id,
        GstProviderSaveRequest request,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var validation = ValidateSaveRequest(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { message = validation });
        }

        var provider = await db.GstApiProviders.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (provider is null)
        {
            return Results.NotFound(new { message = "GST API provider not found." });
        }

        provider.ProviderName = request.ProviderName.Trim();
        provider.ProviderType = request.ProviderType;
        provider.Environment = request.Environment;
        provider.BaseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? null : request.BaseUrl.Trim();
        provider.AuthUrl = string.IsNullOrWhiteSpace(request.AuthUrl) ? null : request.AuthUrl.Trim();
        provider.IsEnabled = request.IsEnabled;
        provider.Priority = request.Priority;
        provider.FallbackEnabled = request.FallbackEnabled;
        provider.TimeoutSeconds = Math.Clamp(request.TimeoutSeconds, 5, 120);
        provider.MaxRetries = Math.Clamp(request.MaxRetries, 0, 5);
        provider.Notes = request.Notes;
        provider.UpdatedBy = context.User.Identity?.Name;
        provider.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        await SaveFeaturesAsync(db, provider.Id, request.Features, cancellationToken);

        var features = await LoadFeaturesAsync(db, [provider.Id], cancellationToken);
        return Results.Ok(ToDetailDto(provider, features));
    }

    private static async Task<IResult> DeleteProviderAsync(
        Guid id,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var provider = await db.GstApiProviders.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (provider is null)
        {
            return Results.NotFound(new { message = "GST API provider not found." });
        }

        var features = await db.GstApiProviderFeatures.Where(f => f.ProviderId == id).ToListAsync(cancellationToken);
        var credentials = await db.GstApiProviderCredentials.Where(c => c.ProviderId == id).ToListAsync(cancellationToken);
        db.GstApiProviderFeatures.RemoveRange(features);
        db.GstApiProviderCredentials.RemoveRange(credentials);
        db.GstApiProviders.Remove(provider);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { message = "GST API provider deleted." });
    }

    private static async Task<IResult> SetEnabledAsync(
        Guid id,
        bool enabled,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var provider = await db.GstApiProviders.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (provider is null)
        {
            return Results.NotFound(new { message = "GST API provider not found." });
        }

        provider.IsEnabled = enabled;
        provider.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { message = enabled ? "Provider enabled." : "Provider disabled." });
    }

    private static async Task<IResult> GetCredentialsAsync(
        Guid id,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var providerExists = await db.GstApiProviders.AsNoTracking().AnyAsync(p => p.Id == id, cancellationToken);
        if (!providerExists)
        {
            return Results.NotFound(new { message = "GST API provider not found." });
        }

        var stored = await db.GstApiProviderCredentials.AsNoTracking()
            .Where(c => c.ProviderId == id)
            .ToDictionaryAsync(c => c.CredentialKey, c => c, cancellationToken);

        var result = GstTaxCatalog.CredentialKeys.Select(key => stored.TryGetValue(key, out var entry)
            ? new GstCredentialEntryDto(key, true, entry.MaskedDisplayValue, entry.UpdatedAt)
            : new GstCredentialEntryDto(key, false, null, null));

        return Results.Ok(result);
    }

    private static async Task<IResult> SaveCredentialsAsync(
        Guid id,
        GstCredentialSaveRequest request,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        GstCredentialProtector protector,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var providerExists = await db.GstApiProviders.AsNoTracking().AnyAsync(p => p.Id == id, cancellationToken);
        if (!providerExists)
        {
            return Results.NotFound(new { message = "GST API provider not found." });
        }

        foreach (var entry in request.Entries)
        {
            if (!GstTaxCatalog.CredentialKeys.Contains(entry.CredentialKey))
            {
                continue;
            }

            var existing = await db.GstApiProviderCredentials
                .FirstOrDefaultAsync(c => c.ProviderId == id && c.CredentialKey == entry.CredentialKey, cancellationToken);

            if (entry.Clear)
            {
                if (existing is not null)
                {
                    db.GstApiProviderCredentials.Remove(existing);
                }

                continue;
            }

            if (string.IsNullOrEmpty(entry.Value))
            {
                // No value submitted and not clearing - leave the stored secret untouched (never re-encrypt a blank).
                continue;
            }

            var encrypted = protector.Protect(entry.Value);
            var masked = GstCredentialProtector.Mask(entry.Value);

            if (existing is null)
            {
                db.GstApiProviderCredentials.Add(new GstApiProviderCredential
                {
                    ProviderId = id,
                    CredentialKey = entry.CredentialKey,
                    EncryptedValue = encrypted,
                    MaskedDisplayValue = masked
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

    private static async Task<IResult> TestHealthAsync(
        Guid id,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var provider = await db.GstApiProviders.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (provider is null)
        {
            return Results.NotFound(new { message = "GST API provider not found." });
        }

        var stopwatch = Stopwatch.StartNew();
        bool success;
        string message;

        if (provider.ProviderType == "LocalMasterOnly")
        {
            await db.GstHsnMasters.AsNoTracking().Select(h => h.Id).Take(1).ToListAsync(cancellationToken);
            success = true;
            message = "Local Master is healthy - HSN/rate/GSTIN cache tables are reachable.";
        }
        else if (!provider.IsEnabled)
        {
            success = false;
            message = "Provider is disabled - enable it first to test connectivity.";
        }
        else
        {
            success = false;
            message = $"Live connectivity testing for provider type '{provider.ProviderType}' arrives in a later GST & Taxes stage (the adapter/HTTP layer isn't wired up yet). Local Master remains fully functional for daily billing.";
        }

        stopwatch.Stop();
        await LogCallAsync(db, provider.Id, "HEALTH_CHECK", success, success ? null : message, (int)stopwatch.ElapsedMilliseconds, cancellationToken);

        return Results.Ok(new GstProviderTestResultDto(success, message, (int)stopwatch.ElapsedMilliseconds, DateTime.UtcNow));
    }

    private static async Task<IResult> TestGstinAsync(
        Guid id,
        GstinTestRequest request,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var provider = await db.GstApiProviders.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (provider is null)
        {
            return Results.NotFound(new { message = "GST API provider not found." });
        }

        var gstin = (request.Gstin ?? string.Empty).Trim().ToUpperInvariant();
        var stopwatch = Stopwatch.StartNew();
        bool success;
        string message;

        if (string.IsNullOrEmpty(gstin))
        {
            success = false;
            message = "Enter a GSTIN to test.";
        }
        else if (!GstinFormatPattern.IsMatch(gstin))
        {
            success = false;
            message = $"'{gstin}' does not match the GSTIN format.";
        }
        else if (provider.ProviderType == "LocalMasterOnly")
        {
            var cached = await db.GstinVerificationCaches.AsNoTracking().FirstOrDefaultAsync(c => c.Gstin == gstin, cancellationToken);
            success = cached is not null;
            message = cached is not null
                ? $"Format valid and found in the local GSTIN cache (last verified {cached.LastVerifiedAt:yyyy-MM-dd})."
                : "Format is valid, but this GSTIN is not yet in the local cache. Verify it once online (Stage GST-3) to cache it.";
        }
        else
        {
            success = false;
            message = $"Format is valid. Live GSTIN lookup for provider type '{provider.ProviderType}' arrives in a later GST & Taxes stage.";
        }

        stopwatch.Stop();
        await LogCallAsync(db, provider.Id, "GSTIN_LOOKUP", success, success ? null : message, (int)stopwatch.ElapsedMilliseconds, cancellationToken, gstin);

        return Results.Ok(new GstProviderTestResultDto(success, message, (int)stopwatch.ElapsedMilliseconds, DateTime.UtcNow));
    }

    private static async Task<IResult> TestHsnAsync(
        Guid id,
        HsnTestRequest request,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var provider = await db.GstApiProviders.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (provider is null)
        {
            return Results.NotFound(new { message = "GST API provider not found." });
        }

        var hsn = (request.HsnCode ?? string.Empty).Trim();
        var stopwatch = Stopwatch.StartNew();
        bool success;
        string message;

        if (string.IsNullOrEmpty(hsn))
        {
            success = false;
            message = "Enter an HSN/SAC code to test.";
        }
        else if (provider.ProviderType == "LocalMasterOnly")
        {
            var found = await db.GstHsnMasters.AsNoTracking().FirstOrDefaultAsync(h => h.HsnCode == hsn, cancellationToken);
            success = found is not null;
            message = found is not null
                ? $"Found in the local HSN master: {found.Description ?? found.CommonTradeDescription ?? "(no description)"}."
                : "Not found in the local HSN master yet. Add it in HSN/SAC Master (Stage GST-4) or import the CBIC HSN Excel.";
        }
        else
        {
            success = false;
            message = $"Live HSN lookup for provider type '{provider.ProviderType}' arrives in a later GST & Taxes stage.";
        }

        stopwatch.Stop();
        await LogCallAsync(db, provider.Id, "HSN_LOOKUP", success, success ? null : message, (int)stopwatch.ElapsedMilliseconds, cancellationToken, hsnCode: hsn);

        return Results.Ok(new GstProviderTestResultDto(success, message, (int)stopwatch.ElapsedMilliseconds, DateTime.UtcNow));
    }

    private static async Task LogCallAsync(
        GarmetixDbContext db,
        Guid? providerId,
        string featureCode,
        bool success,
        string? errorMessage,
        int durationMs,
        CancellationToken cancellationToken,
        string? gstin = null,
        string? hsnCode = null)
    {
        db.GstApiCallLogs.Add(new GstApiCallLog
        {
            ProviderId = providerId,
            FeatureCode = featureCode,
            RequestMethod = "POST",
            IsSuccess = success,
            ErrorMessage = errorMessage,
            DurationMs = durationMs,
            Gstin = gstin,
            HsnCode = hsnCode
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<List<GstApiProviderFeature>> LoadFeaturesAsync(GarmetixDbContext db, Guid[] providerIds, CancellationToken cancellationToken)
    {
        return await db.GstApiProviderFeatures.AsNoTracking()
            .Where(f => providerIds.Contains(f.ProviderId))
            .ToListAsync(cancellationToken);
    }

    private static async Task SaveFeaturesAsync(GarmetixDbContext db, Guid providerId, List<string>? requestedFeatures, CancellationToken cancellationToken)
    {
        var wanted = (requestedFeatures ?? []).Where(code => GstTaxCatalog.FeatureCodes.Contains(code)).Distinct().ToArray();
        var existing = await db.GstApiProviderFeatures.Where(f => f.ProviderId == providerId).ToListAsync(cancellationToken);

        db.GstApiProviderFeatures.RemoveRange(existing.Where(f => !wanted.Contains(f.FeatureCode)));
        foreach (var code in wanted.Where(code => existing.All(f => f.FeatureCode != code)))
        {
            db.GstApiProviderFeatures.Add(new GstApiProviderFeature { ProviderId = providerId, FeatureCode = code, IsEnabled = true });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? ValidateSaveRequest(GstProviderSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProviderName))
        {
            return "Provider name is required.";
        }

        if (!GstTaxCatalog.ProviderTypes.Contains(request.ProviderType))
        {
            return $"'{request.ProviderType}' is not a recognized provider type.";
        }

        if (!GstTaxCatalog.Environments.Contains(request.Environment))
        {
            return $"'{request.Environment}' is not a recognized environment.";
        }

        return null;
    }

    private static GstProviderDetailDto ToDetailDto(GstApiProvider provider, IReadOnlyCollection<GstApiProviderFeature> features) => new(
        provider.Id,
        provider.ProviderName,
        provider.ProviderType,
        provider.Environment,
        provider.BaseUrl,
        provider.AuthUrl,
        provider.IsEnabled,
        provider.Priority,
        provider.FallbackEnabled,
        provider.TimeoutSeconds,
        provider.MaxRetries,
        provider.Notes,
        features.Where(f => f.ProviderId == provider.Id && f.IsEnabled).Select(f => f.FeatureCode).OrderBy(code => code).ToArray(),
        provider.CreatedAt,
        provider.UpdatedAt,
        provider.CreatedBy,
        provider.UpdatedBy);
}

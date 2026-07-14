using System.Security.Claims;
using Garmetix.Api.Workspace;
using Garmetix.Core.Models.FinalAccounts;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Garmetix.Api.FinalAccounts;

public sealed class FinalAccountsSettingsService(
    GarmetixDbContext db,
    IOptions<FinalAccountsOptions> options,
    ILogger<FinalAccountsSettingsService> logger)
{
    private readonly FinalAccountsOptions options = options.Value;

    public async Task<FinalAccountsSettingsResponse> GetAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, null);
        var settings = await FindSettingsAsync(scope, tracking: false, cancellationToken);
        return ToResponse(settings, scope);
    }

    public async Task<FinalAccountsSettingsResponse> SaveAsync(
        FinalAccountsSaveSettingsRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, request);
        var settings = await FindSettingsAsync(scope, tracking: true, cancellationToken);
        if (settings is null)
        {
            settings = new FinalAccountsModuleSettings
            {
                CompanyId = scope.CompanyId,
                StoreGroupId = scope.StoreGroupId,
                StoreId = scope.StoreId,
                CreatedBy = ResolveActor(context)
            };
            db.FinalAccountsModuleSettings.Add(settings);
        }

        ApplyRequest(settings, request);
        settings.UpdatedBy = ResolveActor(context);

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Final Accounts settings saved. Enabled={Enabled} CompanyId={CompanyId} StoreGroupId={StoreGroupId} StoreId={StoreId} PostingMode={PostingMode}",
            settings.Enabled,
            settings.CompanyId,
            settings.StoreGroupId,
            settings.StoreId,
            settings.PostingMode);
        return ToResponse(settings, scope);
    }

    public async Task<bool> IsEnabledAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, null);
        var settings = await FindSettingsAsync(scope, tracking: false, cancellationToken);
        return (settings?.Enabled) ?? options.DefaultEnabled;
    }

    public FinalAccountsSettingsResponse DisabledResponse()
        => ToResponse(null, new FinalAccountsScopeDto(null, null, null));

    private async Task<FinalAccountsModuleSettings?> FindSettingsAsync(
        FinalAccountsScopeDto scope,
        bool tracking,
        CancellationToken cancellationToken)
    {
        var query = db.FinalAccountsModuleSettings
            .Where(item =>
                item.CompanyId == scope.CompanyId
                && item.StoreGroupId == scope.StoreGroupId
                && item.StoreId == scope.StoreId);

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private FinalAccountsSettingsResponse ToResponse(FinalAccountsModuleSettings? settings, FinalAccountsScopeDto scope)
    {
        var enabled = settings?.Enabled ?? options.DefaultEnabled;
        return new FinalAccountsSettingsResponse(
            enabled,
            FinalAccountsSettingsDefaults.FeatureKey,
            FinalAccountsSettingsDefaults.ApiRoot,
            FinalAccountsSettingsDefaults.RouteRoot,
            settings?.CompanyId ?? scope.CompanyId,
            settings?.StoreGroupId ?? scope.StoreGroupId,
            settings?.StoreId ?? scope.StoreId,
            NonBlank(settings?.PostingMode, options.PostingMode, FinalAccountsSettingsDefaults.PostingMode),
            NonBlank(settings?.StatementTemplate, options.StatementTemplate, FinalAccountsSettingsDefaults.StatementTemplate),
            NonBlank(settings?.InventoryValuationMethod, options.InventoryValuationMethod, FinalAccountsSettingsDefaults.InventoryValuationMethod),
            ClampRounding(settings?.RoundingScale ?? options.RoundingScale),
            settings?.AllowHistoricalBackfill ?? false,
            settings?.AllowTallyExport ?? false,
            settings?.AllowProjections ?? false,
            settings?.AllowPeriodReopen ?? false,
            DateTimeOffset.UtcNow,
            enabled
                ? "Final Accounts module is enabled for this scope."
                : "Final Accounts module is installed but disabled. Enable it from setup after closing policy is confirmed.");
    }

    private void ApplyRequest(FinalAccountsModuleSettings settings, FinalAccountsSaveSettingsRequest request)
    {
        settings.Enabled = request.Enabled ?? false;
        settings.PostingMode = Truncate(NonBlank(request.PostingMode, options.PostingMode, FinalAccountsSettingsDefaults.PostingMode), 32);
        settings.StatementTemplate = Truncate(NonBlank(request.StatementTemplate, options.StatementTemplate, FinalAccountsSettingsDefaults.StatementTemplate), 80);
        settings.InventoryValuationMethod = Truncate(NonBlank(request.InventoryValuationMethod, options.InventoryValuationMethod, FinalAccountsSettingsDefaults.InventoryValuationMethod), 48);
        settings.RoundingScale = ClampRounding(request.RoundingScale ?? options.RoundingScale);
        settings.AllowHistoricalBackfill = request.AllowHistoricalBackfill ?? false;
        settings.AllowTallyExport = request.AllowTallyExport ?? false;
        settings.AllowProjections = request.AllowProjections ?? false;
        settings.AllowPeriodReopen = request.AllowPeriodReopen ?? false;
    }

    private static FinalAccountsScopeDto ResolveScope(HttpContext context, FinalAccountsSaveSettingsRequest? request)
    {
        if (WorkspaceScope.HasFullAccess(context))
        {
            return new FinalAccountsScopeDto(request?.CompanyId, request?.StoreGroupId, request?.StoreId);
        }

        return new FinalAccountsScopeDto(
            WorkspaceScope.ClaimGuid(context, "companyId"),
            WorkspaceScope.ClaimGuid(context, "storeGroupId"),
            WorkspaceScope.ClaimGuid(context, "storeId"));
    }

    private static string ResolveActor(HttpContext context)
        => NonBlank(
            context.User.FindFirstValue(ClaimTypes.Name),
            context.User.FindFirstValue("userName"),
            context.User.FindFirstValue(ClaimTypes.NameIdentifier),
            "System");

    private static int ClampRounding(int value) => Math.Clamp(value, 0, 6);

    private static string NonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}

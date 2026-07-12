using Garmetix.Api.Auth;
using Garmetix.Api.Database;
using Garmetix.Api.Workspace;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.GstTax;

public static class GstTaxEndpoints
{
    public static RouteGroupBuilder MapGstTaxEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/gst")
            .WithTags("GST & Taxes")
            .RequireAuthorization(GarmetixPolicies.Gst);

        group.MapGet("/dashboard", GetDashboardAsync);
        group.MapGet("/providers", GetProvidersAsync);

        return group;
    }

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

    private static async Task<IResult> GetProvidersAsync(
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var providers = await db.GstApiProviders.AsNoTracking()
            .OrderBy(p => p.Priority)
            .ToListAsync(cancellationToken);
        var providerIds = providers.Select(p => p.Id).ToArray();
        var featuresByProvider = await db.GstApiProviderFeatures.AsNoTracking()
            .Where(f => providerIds.Contains(f.ProviderId) && f.IsEnabled)
            .ToListAsync(cancellationToken);

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
}

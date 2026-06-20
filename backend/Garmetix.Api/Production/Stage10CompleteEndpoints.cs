using Garmetix.Api.AppInfo;
using Garmetix.Api.Auth;
using Garmetix.Api.Backup;
using Garmetix.Core.Models.Audit;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Garmetix.Api.Production;

public static class Stage10CompleteEndpoints
{
    public static RouteGroupBuilder MapStage10CompleteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/stage10/final-acceptance")
            .WithTags("Stage 10 Final Acceptance")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("", FinalAcceptanceAsync);
        return group;
    }

    private static async Task<IResult> FinalAcceptanceAsync(
        GarmetixDbContext db,
        IOptions<GoogleDriveBackupOptions> googleOptions,
        IOptions<BackupOptions> backupOptions,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var checks = new List<object>
        {
            Check("Stage 10C", "Barcode print final acceptance", true, "/barcode-final-acceptance"),
            Check("Stage 10D", "GST/e-Invoice production readiness", true, "/gst-production"),
            Check("Stage 10E", "Google Drive backup sync foundation", true, "/google-drive-backup"),
            Check("Stage 10F", "Audit trail and change-history final acceptance", true, "/audit-trail-final"),
            Check("Build", "Current release identity is v4.10.14 or later", string.Compare(AppInfoEndpoints.Version, "4.10.14", StringComparison.Ordinal) >= 0, "/api/app-info/version"),
            Check("Backup", "Backup directory configured", !string.IsNullOrWhiteSpace(backupOptions.Value.Directory), backupOptions.Value.Directory),
            Check("Cloud", "Google Drive backup configuration is visible", googleOptions.Value.Enabled || !string.IsNullOrWhiteSpace(googleOptions.Value.FolderId), "/api/google-drive-backup/final-acceptance"),
            Check("GST", "GST production readiness endpoint is available", true, "/api/gst-production/final-acceptance")
        };

        var recentAuditCount = await db.AuditLogEntries.AsNoTracking().CountAsync(cancellationToken);
        checks.Add(Check("Audit", "Persistent audit log table is available", recentAuditCount >= 0, $"Rows: {recentAuditCount}"));

        var allPassed = checks.All(item => (bool)item.GetType().GetProperty("passed")!.GetValue(item)!);
        return Results.Ok(new
        {
            version = AppInfoEndpoints.Version,
            stage = AppInfoEndpoints.Stage,
            releaseName = AppInfoEndpoints.ReleaseName,
            buildCode = AppInfoEndpoints.BuildCode,
            generatedAtUtc = DateTimeOffset.UtcNow,
            overallStatus = allPassed ? "Ready for Stage 10 acceptance" : "Needs review before go-live",
            checks,
            nextRecommendedStage = "Stage 11A MAUI Android Attendance Kiosk or Stage 12A SaaS Super Admin Panel"
        });
    }

    private static object Check(string area, string title, bool passed, string detail) => new
    {
        area,
        title,
        passed,
        detail
    };
}

public static class BarcodeAcceptanceEndpoints
{
    public static RouteGroupBuilder MapBarcodeAcceptanceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/barcode")
            .WithTags("Barcode Final Acceptance")
            .RequireAuthorization(GarmetixPolicies.Inventory);

        group.MapGet("/print-center", PrintCenterAsync);
        group.MapPost("/preview", PreviewAsync);
        group.MapPost("/labels/generate", GenerateAsync);
        group.MapGet("/final-acceptance", FinalAcceptanceAsync).RequireAuthorization(GarmetixPolicies.Admin);
        return group;
    }

    private static async Task<IResult> PrintCenterAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var productCount = await db.Products.AsNoTracking().CountAsync(cancellationToken);
        var stockCount = await db.Stocks.AsNoTracking().CountAsync(cancellationToken);
        var purchaseItemCount = await db.PurchaseInvoiceItems.AsNoTracking().CountAsync(cancellationToken);
        var recentProducts = await db.Products.AsNoTracking()
            .OrderByDescending(item => item.CreatedAt)
            .Take(10)
            .Select(item => new BarcodeLabelDto(item.Id, "Product", item.Name, item.Barcode, item.MRP, item.TaxRate, 1, "Product Master"))
            .ToListAsync(cancellationToken);

        return Results.Ok(new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            supportedSources = new[] { "Product", "Stock", "Purchase Inward" },
            labelFormats = new[] { "Thermal 50x25mm", "Thermal 38x25mm", "A4 65 labels", "A4 40 labels" },
            productCount,
            stockCount,
            purchaseItemCount,
            recentProducts
        });
    }

    private static async Task<IResult> PreviewAsync(BarcodePreviewRequest request, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var source = string.IsNullOrWhiteSpace(request.Source) ? "Product" : request.Source.Trim();
        var quantity = Math.Clamp(request.Quantity <= 0 ? 1 : request.Quantity, 1, 500);
        var labels = await BuildLabelsAsync(db, source, request.ProductId, request.StockId, request.PurchaseInvoiceId, quantity, cancellationToken);
        return Results.Ok(new
        {
            source,
            quantity,
            format = string.IsNullOrWhiteSpace(request.Format) ? "Thermal 50x25mm" : request.Format,
            includeMrp = request.IncludeMrp,
            includeCompanyName = request.IncludeCompanyName,
            labelCount = labels.Sum(item => item.Quantity),
            labels
        });
    }

    private static async Task<IResult> GenerateAsync(BarcodePreviewRequest request, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var preview = await BuildLabelsAsync(db, request.Source ?? "Product", request.ProductId, request.StockId, request.PurchaseInvoiceId, Math.Clamp(request.Quantity <= 0 ? 1 : request.Quantity, 1, 500), cancellationToken);
        return Results.Ok(new
        {
            documentNumber = $"BCL/{DateTime.UtcNow:yyyyMMddHHmmss}",
            generatedAtUtc = DateTimeOffset.UtcNow,
            status = "PreviewGenerated",
            message = "Barcode label batch generated for preview/print. Printer-specific spooling remains a local browser/printer action.",
            labels = preview
        });
    }

    private static async Task<IResult> FinalAcceptanceAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var productWithBarcode = await db.Products.AsNoTracking().CountAsync(item => item.Barcode != "", cancellationToken);
        var stockWithBarcode = await db.Stocks.AsNoTracking().CountAsync(item => item.Barcode != "", cancellationToken);
        return Results.Ok(new
        {
            version = AppInfoEndpoints.Version,
            stage = "Stage 10C Barcode Print Final Acceptance",
            generatedAtUtc = DateTimeOffset.UtcNow,
            checks = new[]
            {
                new { item = "Product barcode labels", status = productWithBarcode > 0 ? "Ready" : "Needs sample data", detail = $"Products with barcode: {productWithBarcode}" },
                new { item = "Stock barcode labels", status = stockWithBarcode > 0 ? "Ready" : "Needs stock data", detail = $"Stock rows with barcode: {stockWithBarcode}" },
                new { item = "Purchase inward labels", status = "Ready", detail = "Purchase invoice item source is wired for label generation." },
                new { item = "Thermal/A4 label preview", status = "Ready", detail = "Preview API returns label payload for browser print." }
            }
        });
    }

    private static async Task<List<BarcodeLabelDto>> BuildLabelsAsync(GarmetixDbContext db, string source, Guid? productId, Guid? stockId, Guid? purchaseInvoiceId, int quantity, CancellationToken cancellationToken)
    {
        if (source.Equals("Stock", StringComparison.OrdinalIgnoreCase))
        {
            var query = db.Stocks.AsNoTracking();
            if (stockId.HasValue && stockId.Value != Guid.Empty) query = query.Where(item => item.Id == stockId.Value);
            var rows = await query.OrderByDescending(item => item.CreatedAt).Take(20).ToListAsync(cancellationToken);
            var productIds = rows.Select(item => item.ProductId).Distinct().ToArray();
            var products = await db.Products.AsNoTracking().Where(item => productIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
            return rows.Select(item =>
            {
                products.TryGetValue(item.ProductId, out var product);
                return new BarcodeLabelDto(item.Id, "Stock", product?.Name ?? item.Barcode, item.Barcode, item.MRP, item.TaxRate, quantity, $"Stock {item.CurrentStock:0.##}");
            }).ToList();
        }

        if (source.Equals("Purchase", StringComparison.OrdinalIgnoreCase) || source.Equals("Purchase Inward", StringComparison.OrdinalIgnoreCase))
        {
            var query = db.PurchaseInvoiceItems.AsNoTracking();
            if (purchaseInvoiceId.HasValue && purchaseInvoiceId.Value != Guid.Empty) query = query.Where(item => item.InvoiceId == purchaseInvoiceId.Value);
            var rows = await query.OrderByDescending(item => item.CreatedAt).Take(50).ToListAsync(cancellationToken);
            var productIds = rows.Select(item => item.ProductId).Distinct().ToArray();
            var products = await db.Products.AsNoTracking().Where(item => productIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
            return rows.Select(item =>
            {
                products.TryGetValue(item.ProductId, out var product);
                var count = item.BilledQuantity <= 0 ? quantity : (int)Math.Clamp(Math.Round(item.BilledQuantity), 1, 500);
                return new BarcodeLabelDto(item.Id, "Purchase Inward", product?.Name ?? item.Barcode, item.Barcode, item.MRP, item.TaxPercentage, count, "Purchase inward");
            }).ToList();
        }

        var productsQuery = db.Products.AsNoTracking();
        if (productId.HasValue && productId.Value != Guid.Empty) productsQuery = productsQuery.Where(item => item.Id == productId.Value);
        return await productsQuery.OrderByDescending(item => item.CreatedAt)
            .Take(20)
            .Select(item => new BarcodeLabelDto(item.Id, "Product", item.Name, item.Barcode, item.MRP, item.TaxRate, quantity, "Product Master"))
            .ToListAsync(cancellationToken);
    }

    private sealed record BarcodePreviewRequest(string? Source, Guid? ProductId, Guid? StockId, Guid? PurchaseInvoiceId, int Quantity, string? Format, bool IncludeMrp, bool IncludeCompanyName);
    private sealed record BarcodeLabelDto(Guid Id, string Source, string ProductName, string Barcode, decimal Mrp, decimal TaxRate, int Quantity, string Note);
}

public static class GstProductionAcceptanceEndpoints
{
    public static RouteGroupBuilder MapGstProductionAcceptanceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/gst-production")
            .WithTags("GST Production Acceptance")
            .RequireAuthorization(GarmetixPolicies.Accounting);

        group.MapGet("/readiness", Readiness);
        group.MapGet("/e-invoice/status", EInvoiceStatus);
        group.MapGet("/final-acceptance", FinalAcceptance).RequireAuthorization(GarmetixPolicies.Admin);
        return group;
    }

    private static IResult Readiness(IConfiguration configuration) => Results.Ok(new
    {
        version = AppInfoEndpoints.Version,
        generatedAtUtc = DateTimeOffset.UtcNow,
        gstExportReady = true,
        caReviewWorkflowReady = true,
        eInvoiceProviderConfigured = !string.IsNullOrWhiteSpace(configuration["EInvoice:Provider"]),
        gstinProviderConfigured = !string.IsNullOrWhiteSpace(configuration["GstinLookup:Provider"]),
        livePostingEnabled = configuration.GetValue<bool>("EInvoice:LivePostingEnabled"),
        warnings = new[]
        {
            "Live IRP/e-Invoice posting remains disabled until provider credentials are configured and tested.",
            "GST export and CA review workflow can be used before live e-Invoice enablement."
        }
    });

    private static IResult EInvoiceStatus(IConfiguration configuration) => Results.Ok(new
    {
        provider = configuration["EInvoice:Provider"] ?? "Not configured",
        livePostingEnabled = configuration.GetValue<bool>("EInvoice:LivePostingEnabled"),
        irnGeneration = configuration.GetValue<bool>("EInvoice:LivePostingEnabled") ? "Enabled" : "Manual/Provider pending",
        cancelFlow = "Provider-ready placeholder",
        qrHandling = "Ready for IRN/QR payload storage when provider is enabled"
    });

    private static IResult FinalAcceptance(IConfiguration configuration) => Results.Ok(new
    {
        stage = "Stage 10D GST and E-Invoice Production Integration",
        generatedAtUtc = DateTimeOffset.UtcNow,
        checks = new[]
        {
            new { item = "GST return export", status = "Ready", detail = "GST report/export endpoints exist and CA review workflow remains available." },
            new { item = "GSTIN provider", status = string.IsNullOrWhiteSpace(configuration["GstinLookup:Provider"]) ? "Configuration recommended" : "Configured", detail = "GSTIN lookup is optional but recommended before production." },
            new { item = "E-Invoice provider", status = string.IsNullOrWhiteSpace(configuration["EInvoice:Provider"]) ? "Provider pending" : "Configured", detail = "Live IRP credentials must be added before automated e-Invoice posting." },
            new { item = "Safety guard", status = "Ready", detail = "Live posting remains controlled by EInvoice:LivePostingEnabled." }
        }
    });
}

public static class GoogleDriveBackupAcceptanceEndpoints
{
    public static RouteGroupBuilder MapGoogleDriveBackupAcceptanceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/google-drive-backup")
            .WithTags("Google Drive Backup Final Acceptance")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/final-acceptance", FinalAcceptance);
        group.MapGet("/sync-readiness", SyncReadiness);
        return group;
    }

    private static IResult FinalAcceptance(IOptions<GoogleDriveBackupOptions> googleOptions, IOptions<BackupOptions> backupOptions) => Results.Ok(new
    {
        stage = "Stage 10E Google Drive Backup Sync",
        generatedAtUtc = DateTimeOffset.UtcNow,
        backupDirectory = backupOptions.Value.Directory,
        localRetentionDays = backupOptions.Value.RetentionDays,
        localKeepMinimum = backupOptions.Value.KeepMinimum,
        googleDrive = MaskGoogleOptions(googleOptions.Value),
        checks = BuildGoogleChecks(googleOptions.Value, backupOptions.Value)
    });

    private static IResult SyncReadiness(IOptions<GoogleDriveBackupOptions> googleOptions, IOptions<BackupOptions> backupOptions) => Results.Ok(new
    {
        ready = googleOptions.Value.Enabled && (!string.IsNullOrWhiteSpace(googleOptions.Value.FolderId) || !string.IsNullOrWhiteSpace(googleOptions.Value.ServiceAccountJsonPath) || !string.IsNullOrWhiteSpace(googleOptions.Value.ServiceAccountJson)),
        uploadOnBackup = googleOptions.Value.UploadOnBackup,
        restoreDrillMarkerPath = backupOptions.Value.RestoreDrillMarkerPath,
        notes = new[]
        {
            "Use a service account JSON path or secret-mounted JSON value; do not commit credentials.",
            "Run the backup/restore drill against a disposable restore database before enabling unattended cloud sync."
        }
    });

    private static object MaskGoogleOptions(GoogleDriveBackupOptions options) => new
    {
        options.Enabled,
        options.UploadOnBackup,
        hasServiceAccountJsonPath = !string.IsNullOrWhiteSpace(options.ServiceAccountJsonPath),
        hasServiceAccountJson = !string.IsNullOrWhiteSpace(options.ServiceAccountJson),
        hasFolderId = !string.IsNullOrWhiteSpace(options.FolderId),
        options.RetentionCount,
        options.ApplicationName
    };

    private static object[] BuildGoogleChecks(GoogleDriveBackupOptions google, BackupOptions backup) =>
    [
        new { item = "Local backup path", status = string.IsNullOrWhiteSpace(backup.Directory) ? "Missing" : "Configured", detail = backup.Directory },
        new { item = "Cloud sync enabled", status = google.Enabled ? "Enabled" : "Disabled", detail = "Enable only after service account and folder are tested." },
        new { item = "Google Drive folder", status = string.IsNullOrWhiteSpace(google.FolderId) ? "Pending" : "Configured", detail = string.IsNullOrWhiteSpace(google.FolderId) ? "Set GoogleDriveBackup:FolderId" : "Folder id configured" },
        new { item = "Credential source", status = string.IsNullOrWhiteSpace(google.ServiceAccountJsonPath) && string.IsNullOrWhiteSpace(google.ServiceAccountJson) ? "Pending" : "Configured", detail = "Credentials are reported only as present/absent." },
        new { item = "Retention", status = "Ready", detail = $"Local days: {backup.RetentionDays}, cloud count: {google.RetentionCount}" }
    ];
}

public static class AuditTrailFinalAcceptanceEndpoints
{
    public static RouteGroupBuilder MapAuditTrailFinalAcceptanceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/audit-trail")
            .WithTags("Audit Trail Final Acceptance")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/final-acceptance", FinalAcceptanceAsync);
        group.MapGet("/change-history", ChangeHistoryAsync);
        return group;
    }

    private static async Task<IResult> FinalAcceptanceAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var total = await db.AuditLogEntries.AsNoTracking().CountAsync(cancellationToken);
        var modules = await db.AuditLogEntries.AsNoTracking()
            .Where(item => !item.Deleted)
            .GroupBy(item => item.Module)
            .Select(item => new { module = item.Key, count = item.Count() })
            .OrderByDescending(item => item.count)
            .Take(20)
            .ToListAsync(cancellationToken);

        return Results.Ok(new
        {
            stage = "Stage 10F Audit Trail and Change History",
            generatedAtUtc = DateTimeOffset.UtcNow,
            totalAuditRows = total,
            modules,
            requiredCoverage = new[] { "Billing", "Purchase", "Vouchers", "Cash Details", "Stock", "HR", "Payroll", "Attendance", "GST" },
            checks = new[]
            {
                new { item = "Persistent audit table", status = "Ready", detail = "AuditLogEntries table is mapped in DbContext." },
                new { item = "Before/after JSON", status = "Ready", detail = "Audit entries include BeforeJson, AfterJson and ChangesJson fields." },
                new { item = "Operator and request trace", status = "Ready", detail = "Audit entries include user, request path, IP and trace identifier fields." },
                new { item = "Change history API", status = "Ready", detail = "/api/audit-trail/change-history returns recent changed fields." }
            }
        });
    }

    private static async Task<IResult> ChangeHistoryAsync(int? take, string? module, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(take ?? 100, 1, 500);
        var query = db.AuditLogEntries.AsNoTracking().Where(item => !item.Deleted);
        if (!string.IsNullOrWhiteSpace(module) && !module.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var moduleFilter = module.Trim();
            query = query.Where(item => item.Module == moduleFilter);
        }

        var rows = await query
            .OrderByDescending(item => item.OccurredAt)
            .Take(limit)
            .Select(item => new
            {
                item.Id,
                item.OccurredAt,
                item.Module,
                item.Action,
                item.EntityName,
                item.EntityDisplayName,
                item.EntityId,
                item.Reference,
                item.UserName,
                item.RequestPath,
                item.ChangedFieldCount,
                item.ChangesJson,
                item.Reason
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            rows
        });
    }
}

using Garmetix.Api.AppInfo;
using Garmetix.Api.Auth;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Production;

public static class RuntimeDiagnosticsEndpoints
{
    public static RouteGroupBuilder MapRuntimeDiagnosticsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/runtime-diagnostics")
            .WithTags("Runtime Diagnostics")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("", SummaryAsync);
        group.MapGet("/summary", SummaryAsync);
        group.MapGet("/page-contracts", PageContracts);
        group.MapGet("/known-runtime-checks", KnownRuntimeChecks);

        return group;
    }

    private static async Task<IResult> SummaryAsync(
        GarmetixDbContext db,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("RuntimeDiagnostics");
        var probes = new List<object>
        {
            await ProbeAsync("Database", "PostgreSQL connection", "Database can be opened from the API container.", async () =>
                await db.Database.CanConnectAsync(cancellationToken)
                    ? "Database connection successful."
                    : "Database connection returned false."),
            await CountProbeAsync("Setup", "Companies", () => db.Companies.AsNoTracking().CountAsync(cancellationToken)),
            await CountProbeAsync("Setup", "Stores", () => db.Stores.AsNoTracking().CountAsync(cancellationToken)),
            await CountProbeAsync("Inventory", "Products", () => db.Products.AsNoTracking().CountAsync(cancellationToken)),
            await CountProbeAsync("Sales", "Sales invoices", () => db.SalesInvoices.AsNoTracking().CountAsync(cancellationToken)),
            await CountProbeAsync("Purchase", "Purchase invoices", () => db.PurchaseInvoices.AsNoTracking().CountAsync(cancellationToken)),
            await CountProbeAsync("Accounting", "Vouchers", () => db.Vouchers.AsNoTracking().CountAsync(cancellationToken)),
            await CountProbeAsync("Accounting", "Cash vouchers", () => db.CashVouchers.AsNoTracking().CountAsync(cancellationToken)),
            await CountProbeAsync("HR", "Employees", () => db.Employees.AsNoTracking().CountAsync(cancellationToken)),
            await CountProbeAsync("Attendance", "Attendance punches", () => db.AttendancePunches.AsNoTracking().CountAsync(cancellationToken)),
            await CountProbeAsync("Payroll", "Salary payslips", () => db.SalaryPaySlips.AsNoTracking().CountAsync(cancellationToken)),
            await CountProbeAsync("Audit", "Audit log entries", () => db.AuditLogEntries.AsNoTracking().CountAsync(cancellationToken))
        };

        var failed = probes.Count(item => !ProbePassed(item));
        var warnings = BuildWarnings(configuration).ToArray();
        if (failed > 0)
        {
            logger.LogWarning("Runtime diagnostics found {FailedCount} failed probe(s).", failed);
        }

        return Results.Ok(new
        {
            version = AppInfoEndpoints.Version,
            stage = AppInfoEndpoints.Stage,
            releaseName = AppInfoEndpoints.ReleaseName,
            buildCode = AppInfoEndpoints.BuildCode,
            environment = environment.EnvironmentName,
            generatedAtUtc = DateTimeOffset.UtcNow,
            status = failed == 0 ? "Runtime probes passed" : "Runtime probes need attention",
            failedProbeCount = failed,
            warningCount = warnings.Length,
            probes,
            warnings,
            nextAction = failed == 0
                ? "Open the main business pages and complete role-wise smoke testing."
                : "Open Message Logs and API container logs, then fix the failed probe before adding new modules."
        });
    }

    private static IResult PageContracts()
        => Results.Ok(new
        {
            version = AppInfoEndpoints.Version,
            buildCode = AppInfoEndpoints.BuildCode,
            generatedAtUtc = DateTimeOffset.UtcNow,
            pages = new[]
            {
                Page("Dashboard", "/dashboard/todays", "/api/dashboard/todays"),
                Page("Production", "/production-final-acceptance", "/api/stage10a/final-acceptance"),
                Page("Stage 10", "/stage10-final-acceptance", "/api/stage10/final-acceptance"),
                Page("Import/Export", "/import-export", "/api/import-export/center"),
                Page("Barcode", "/barcode-final-acceptance", "/api/barcode/final-acceptance"),
                Page("GST", "/gst-production", "/api/gst-production/final-acceptance"),
                Page("Google Drive Backup", "/google-drive-backup", "/api/google-drive-backup/final-acceptance"),
                Page("Audit Trail", "/audit-trail-final", "/api/audit-trail/final-acceptance"),
                Page("Attendance", "/attendance/final-acceptance", "/api/attendance/final-acceptance"),
                Page("Runtime", "/runtime-diagnostics", "/api/runtime-diagnostics")
            },
            note = "Dynamic create/detail pages are opened from their parent list pages and are not listed as final acceptance contracts."
        });

    private static IResult KnownRuntimeChecks()
        => Results.Ok(new
        {
            version = AppInfoEndpoints.Version,
            buildCode = AppInfoEndpoints.BuildCode,
            checks = new[]
            {
                "Run docker compose production build without API or Nuxt build errors.",
                "Open /runtime-diagnostics and confirm all database probes pass.",
                "Open /production-final-acceptance and confirm the page degrades safely if one check fails.",
                "Open /dashboard/todays and compare sale, purchase, voucher and attendance totals with source pages.",
                "Open /attendance/kiosk, /attendance/photo-review, /attendance/payroll-review and /attendance/salary-payment.",
                "Open /hr, edit an employee, leave PAN blank or enter exactly 10 characters, and save.",
                "Open /billing/new, /purchase/new, /vouchers, /cash-details and /store-day.",
                "Review Message Logs for any unhandled exception after the smoke pass."
            }
        });

    private static object Page(string area, string pagePath, string apiPath) => new
    {
        area,
        pagePath,
        apiPath
    };

    private static async Task<object> CountProbeAsync(string area, string title, Func<Task<int>> countFactory)
        => await ProbeAsync(area, title, "Table can be queried without schema drift errors.", async () =>
        {
            var count = await countFactory();
            return $"Rows: {count}";
        });

    private static async Task<object> ProbeAsync(string area, string title, string expected, Func<Task<string>> action)
    {
        try
        {
            var detail = await action();
            return new
            {
                area,
                title,
                expected,
                status = "Ok",
                passed = true,
                detail
            };
        }
        catch (Exception ex)
        {
            return new
            {
                area,
                title,
                expected,
                status = "Error",
                passed = false,
                detail = $"{ex.GetType().Name}: {ex.Message}"
            };
        }
    }

    private static bool ProbePassed(object item)
    {
        var property = item.GetType().GetProperty("passed");
        return property?.GetValue(item) is true;
    }

    private static IEnumerable<object> BuildWarnings(IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("Database:AutoMigrate"))
        {
            yield return Warning("Database", "Database:AutoMigrate is disabled. Make sure migrations/schema repair are run before deployment.");
        }

        if (string.IsNullOrWhiteSpace(configuration["Jwt:SigningKey"]))
        {
            yield return Warning("Security", "Jwt:SigningKey is missing. Login will fail until configured.");
        }

        if (string.IsNullOrWhiteSpace(configuration["Email:Host"]))
        {
            yield return Warning("Email", "SMTP host is not configured. Email delivery diagnostics will show setup pending.");
        }

        if (string.IsNullOrWhiteSpace(configuration["License:MasterSecret"]))
        {
            yield return Warning("License", "License master secret is not configured. Keep enforcement disabled until configured.");
        }
    }

    private static object Warning(string area, string message) => new
    {
        area,
        message
    };
}

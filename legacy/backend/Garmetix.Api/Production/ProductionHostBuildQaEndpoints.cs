using System.Text;
using Garmetix.Api.AppInfo;
using Garmetix.Api.Auth;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Production;

public static class ProductionHostBuildQaEndpoints
{
    public static RouteGroupBuilder MapProductionHostBuildQaEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/production-host-build-qa")
            .WithTags("Production Host Build QA")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("", BuildAsync);
        group.MapGet("/evidence.csv", ExportCsvAsync);

        return group;
    }

    private static async Task<ProductionHostBuildQaReportDto> BuildAsync(
        GarmetixDbContext db,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var checks = new List<ProductionHostBuildQaCheckDto>();

        await AddProbeAsync(checks, "DATABASE_CONNECT", "API can connect to PostgreSQL", "Critical", async () =>
            await db.Database.CanConnectAsync(cancellationToken) ? "Database connection successful." : throw new InvalidOperationException("Database connection returned false."));
        await AddCountProbeAsync(checks, "COMPANY_TABLE", "Company table query", "Critical", () => db.Companies.AsNoTracking().CountAsync(cancellationToken));
        await AddCountProbeAsync(checks, "STORE_TABLE", "Store table query", "Critical", () => db.Stores.AsNoTracking().CountAsync(cancellationToken));
        await AddCountProbeAsync(checks, "USER_TABLE", "User table query", "Critical", () => db.Users.AsNoTracking().CountAsync(cancellationToken));
        await AddCountProbeAsync(checks, "SALE_TABLE", "Sales invoice table query", "Critical", () => db.SalesInvoices.AsNoTracking().CountAsync(cancellationToken));
        await AddCountProbeAsync(checks, "PURCHASE_TABLE", "Purchase invoice table query", "Critical", () => db.PurchaseInvoices.AsNoTracking().CountAsync(cancellationToken));
        await AddCountProbeAsync(checks, "PURCHASE_RETURN_TABLE", "Purchase return table query", "Critical", () => db.PurchaseReturns.AsNoTracking().CountAsync(cancellationToken));
        await AddCountProbeAsync(checks, "PURCHASE_RETURN_ITC_TABLE", "Purchase return ITC reversal table query", "Critical", () => db.PurchaseReturnItcReversals.AsNoTracking().CountAsync(cancellationToken));
        await AddCountProbeAsync(checks, "VENDOR_SETTLEMENT_TABLE", "Vendor settlement table query", "Critical", () => db.VendorSettlements.AsNoTracking().CountAsync(cancellationToken));
        await AddCountProbeAsync(checks, "JOURNAL_TABLE", "Journal entry table query", "Critical", () => db.JournalEntries.AsNoTracking().CountAsync(cancellationToken));
        await AddCountProbeAsync(checks, "BANK_TABLE", "Bank transaction table query", "Critical", () => db.BankTransactions.AsNoTracking().CountAsync(cancellationToken));
        await AddCountProbeAsync(checks, "AUDIT_TABLE", "Audit log table query", "Warning", () => db.AuditLogEntries.AsNoTracking().CountAsync(cancellationToken));

        AddEnvironmentChecks(checks, environment, configuration);
        AddOperatorPendingChecks(checks);

        var critical = checks.Count(item => item.Status == "Critical");
        var warnings = checks.Count(item => item.Status == "Warning");
        var passed = checks.Count(item => item.Status == "Pass");
        var status = critical > 0 ? "Blocked" : warnings > 0 ? "Needs Manual QA" : "Ready";

        return new ProductionHostBuildQaReportDto(
            status,
            AppInfoEndpoints.Version,
            AppInfoEndpoints.Stage,
            AppInfoEndpoints.BuildCode,
            environment.EnvironmentName,
            DateTimeOffset.UtcNow,
            passed,
            warnings,
            critical,
            checks,
            CloseoutChecklist(status),
            OperatorRules(),
            KnownLimitations(),
            NextModuleCandidates());
    }

    private static async Task<IResult> ExportCsvAsync(
        GarmetixDbContext db,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var report = await BuildAsync(db, environment, configuration, cancellationToken);
        var csv = BuildCsv(report);
        return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", $"garmetix-production-host-build-qa-{DateTime.UtcNow:yyyyMMddHHmm}.csv");
    }

    private static async Task AddProbeAsync(List<ProductionHostBuildQaCheckDto> checks, string code, string label, string failureSeverity, Func<Task<string>> probe)
    {
        try
        {
            var detail = await probe();
            checks.Add(new ProductionHostBuildQaCheckDto(code, label, "Pass", "Runtime", detail, "No action required."));
        }
        catch (Exception ex)
        {
            checks.Add(new ProductionHostBuildQaCheckDto(code, label, failureSeverity, "Runtime", $"{ex.GetType().Name}: {ex.Message}", "Check docker API logs, run database repair/migration, then rerun this QA."));
        }
    }

    private static Task AddCountProbeAsync(List<ProductionHostBuildQaCheckDto> checks, string code, string label, string failureSeverity, Func<Task<int>> countFactory)
        => AddProbeAsync(checks, code, label, failureSeverity, async () => $"Rows: {await countFactory()}");

    private static void AddEnvironmentChecks(List<ProductionHostBuildQaCheckDto> checks, IWebHostEnvironment environment, IConfiguration configuration)
    {
        checks.Add(new ProductionHostBuildQaCheckDto(
            "ASPNETCORE_ENVIRONMENT",
            "API environment mode",
            environment.IsProduction() ? "Pass" : "Warning",
            "Configuration",
            environment.IsProduction() ? "API is running in Production." : $"API is running in {environment.EnvironmentName}.",
            "Set ASPNETCORE_ENVIRONMENT=Production for live use."));

        var cors = configuration["Cors:AllowedOrigins"] ?? configuration["CORS_ALLOWED_ORIGINS"] ?? string.Empty;
        checks.Add(new ProductionHostBuildQaCheckDto(
            "CORS_ALLOWED_ORIGINS",
            "Frontend CORS origin configured",
            string.IsNullOrWhiteSpace(cors) || cors.Contains("localhost", StringComparison.OrdinalIgnoreCase) ? "Warning" : "Pass",
            "Configuration",
            string.IsNullOrWhiteSpace(cors) ? "CORS allowed origins are empty or using defaults." : $"Configured: {cors}",
            "Use the exact HTTPS frontend origin before live billing."));

        var jwt = configuration["Jwt:SigningKey"] ?? string.Empty;
        checks.Add(new ProductionHostBuildQaCheckDto(
            "JWT_SIGNING_KEY",
            "JWT signing key configured",
            jwt.Length >= 32 ? "Pass" : "Critical",
            "Security",
            jwt.Length >= 32 ? "JWT signing key length looks acceptable." : "JWT signing key is missing or too short.",
            "Set a strong random JWT_SIGNING_KEY in machine.env/.env.production."));
    }

    private static void AddOperatorPendingChecks(List<ProductionHostBuildQaCheckDto> checks)
    {
        foreach (var check in new[]
        {
            ("DOCKER_BUILD", "docker compose up --build completed without API/Nuxt error", "Run docker compose up --build on the production host and save the terminal log."),
            ("API_HEALTH", "/api/health returns healthy through the public domain", "Open the hosted health endpoint and verify API container is not restarting."),
            ("LOGIN_SMOKE", "Admin login and role login smoke test", "Login as admin/owner/store manager/accountant and verify dashboard routing."),
            ("MENU_SMOKE", "Sidebar opens all newly added closure pages", "Open purchase return settlement QA, owner sign-off, go-live gate, closeout and bank reco pages."),
            ("CSV_EXPORT_SMOKE", "CSV exports download from closeout pages", "Download CSV from at least purchase return settlement, owner closeout and go-live gate."),
            ("PDF_PRINT_SMOKE", "Sale/purchase/return PDFs open from hosted domain", "Open sale invoice, purchase invoice and purchase return PDF from the hosted frontend."),
            ("BACKUP_RESTORE_DRILL", "Backup and restore proof completed", "Run backup script, restore on test system and record proof reference."),
            ("MESSAGE_LOG_REVIEW", "Message Logs reviewed after smoke pass", "Review frontend/API message logs after the smoke pass and fix all unhandled errors.")
        })
        {
            checks.Add(new ProductionHostBuildQaCheckDto(check.Item1, check.Item2, "Warning", "Manual QA", "Manual proof not stored in this automatic probe.", check.Item3));
        }
    }

    private static IReadOnlyList<string> CloseoutChecklist(string status)
    {
        var lines = new List<string>
        {
            "Run docker compose up --build on the real production host and save the build log.",
            "Open /api/health, /api/app-info/version and this Production Host Build QA page from the public domain.",
            "Login as every production role and check dashboard routing and sidebar visibility.",
            "Open all go-live/closeout pages and confirm no 500 errors appear in Message Logs.",
            "Download CSV evidence from critical closeout pages and open it in Excel.",
            "Open sale, purchase, purchase return and owner sign-off PDFs from the hosted frontend.",
            "Run backup and restore drill on a test system before final owner sign-off."
        };
        if (status != "Ready") lines.Insert(0, "Status is not Ready. Complete runtime/manual QA before live billing.");
        return lines;
    }

    private static IReadOnlyList<string> OperatorRules() => new[]
    {
        "Do not treat sandbox/static validation as production approval; the final proof must come from the live Docker host.",
        "Do not use npm audit fix --force or package upgrades during go-live unless a specific build error requires it.",
        "Keep the exact ZIP, build log, backup file and owner sign-off PDF together for rollback/audit.",
        "Fix compile/runtime errors first; do not continue adding business features on top of a broken production build."
    };

    private static IReadOnlyList<string> KnownLimitations() => new[]
    {
        "This page can query runtime/database health, but it cannot prove that an external terminal command was executed unless the operator records evidence.",
        "Manual printer, browser download and restore-drill proof must still be performed by the operator on the actual deployment machine.",
        "Warnings are intentionally kept until manual QA evidence is completed."
    };

    private static IReadOnlyList<string> NextModuleCandidates() => new[]
    {
        "Automated regression test suite for core billing/purchase/accounting flows",
        "Purchase import OCR provider production tuning",
        "Biometric attendance hardware integration",
        "Performance/index tuning after real-data volume testing"
    };

    private static string BuildCsv(ProductionHostBuildQaReportDto report)
    {
        var sb = new StringBuilder();
        void Row(params object?[] values) => sb.AppendLine(string.Join(',', values.Select(Csv)));
        Row("Garmetix Production Host Build QA");
        Row("Status", report.Status, "Version", report.Version, "Stage", report.Stage, "Build", report.BuildCode, "Environment", report.Environment, "Generated", report.GeneratedAtUtc);
        Row("Passed", report.Passed, "Warnings", report.Warnings, "Critical", report.Critical);
        Row();
        Row("Checks");
        Row("Code", "Label", "Status", "Area", "Detail", "Action");
        foreach (var check in report.Checks) Row(check.Code, check.Label, check.Status, check.Area, check.Detail, check.Action);
        Row();
        Row("Closeout Checklist");
        foreach (var item in report.CloseoutChecklist) Row(item);
        return sb.ToString();
    }

    private static string Csv(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            DateTimeOffset date => date.ToString("O"),
            _ => value.ToString() ?? string.Empty
        };
        return $"\"{text.Replace("\"", "\"\"")}\"";
    }
}

public record ProductionHostBuildQaReportDto(
    string Status,
    string Version,
    string Stage,
    string BuildCode,
    string Environment,
    DateTimeOffset GeneratedAtUtc,
    int Passed,
    int Warnings,
    int Critical,
    IReadOnlyList<ProductionHostBuildQaCheckDto> Checks,
    IReadOnlyList<string> CloseoutChecklist,
    IReadOnlyList<string> OperatorRules,
    IReadOnlyList<string> KnownLimitations,
    IReadOnlyList<string> NextModuleCandidates);

public record ProductionHostBuildQaCheckDto(
    string Code,
    string Label,
    string Status,
    string Area,
    string Detail,
    string Action);

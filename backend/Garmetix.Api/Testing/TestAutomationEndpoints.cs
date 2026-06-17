using Garmetix.Api.AppInfo;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Testing;

public static class TestAutomationEndpoints
{
    public static RouteGroupBuilder MapTestAutomationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/test-automation")
            .WithTags("Test Automation")
            .AllowAnonymous();

        group.MapGet("/manifest", Manifest);
        group.MapGet("/runtime-smoke", RuntimeSmokeAsync);

        return group;
    }

    private static IResult Manifest()
        => Results.Ok(new
        {
            version = AppInfoEndpoints.Version,
            buildCode = AppInfoEndpoints.BuildCode,
            stage = AppInfoEndpoints.Stage,
            checks = TestAutomationCatalog.BuildDefinitions()
        });

    private static async Task<IResult> RuntimeSmokeAsync(
        GarmetixDbContext db,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        var checks = new List<TestAutomationRuntimeCheckDto>();

        Add(checks,
            "APP_VERSION",
            "Application version endpoint contract",
            AppInfoEndpoints.Version.StartsWith("4.", StringComparison.Ordinal) && AppInfoEndpoints.BuildCode.StartsWith("GARMETIX-8", StringComparison.Ordinal) ? "Pass" : "Critical",
            "High",
            $"API reports {AppInfoEndpoints.Version} / {AppInfoEndpoints.BuildCode} in {environment.EnvironmentName}.",
            "Rebuild and redeploy the latest Stage 8G release archive if version/build code is stale.");

        try
        {
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            Add(checks,
                "DATABASE_CONNECTIVITY",
                "Database connectivity",
                canConnect ? "Pass" : "Critical",
                "High",
                canConnect ? "API can connect to PostgreSQL." : "API cannot connect to PostgreSQL.",
                "Check postgres container health, connection string, Docker network and POSTGRES_PASSWORD.");
        }
        catch (Exception ex)
        {
            Add(checks,
                "DATABASE_CONNECTIVITY",
                "Database connectivity",
                "Critical",
                "High",
                $"Database connectivity check threw {ex.GetType().Name}.",
                "Open API logs and fix the database startup error before using the app.");
        }

        try
        {
            await db.AuditLogEntries.AsNoTracking().Take(1).CountAsync(cancellationToken);
            Add(checks,
                "AUDIT_TABLE",
                "Audit table availability",
                "Pass",
                "Medium",
                "AuditLogEntries table is queryable.",
                "Run /api/database/repair or redeploy if AuditLogEntries is missing.");
        }
        catch (Exception ex)
        {
            Add(checks,
                "AUDIT_TABLE",
                "Audit table availability",
                "Critical",
                "High",
                $"AuditLogEntries query failed with {ex.GetType().Name}.",
                "Run /api/database/repair, check schema bootstrap mode, or reset only a disposable test database.");
        }

        var definitions = TestAutomationCatalog.BuildDefinitions();
        var requiredCodes = new[]
        {
            "BACKEND_UNIT_TESTS",
            "FRONTEND_BUILD",
            "FRONTEND_SMOKE",
            "DOCKER_COMPOSE_BUILD",
            "DOCKER_HEALTH",
            "AUTHENTICATED_API_SMOKE"
        };
        var catalogOk = requiredCodes.All(code => definitions.Any(item => item.Code == code));
        Add(checks,
            "TEST_MANIFEST",
            "Automated test manifest",
            catalogOk ? "Pass" : "Warning",
            catalogOk ? "Info" : "Medium",
            catalogOk ? $"Manifest exposes {definitions.Count} test definitions." : "Manifest is missing one or more required smoke-test definitions.",
            "Restore the Stage 8F Package 2 TestAutomationCatalog definitions.");

        var critical = checks.Count(item => item.Status.Equals("Critical", StringComparison.OrdinalIgnoreCase));
        var warnings = checks.Count(item => item.Status.Equals("Warning", StringComparison.OrdinalIgnoreCase));
        var passed = checks.Count(item => item.Status.Equals("Pass", StringComparison.OrdinalIgnoreCase));
        var overall = critical > 0 ? "Blocked" : warnings > 0 ? "Needs attention" : "Pass";

        return Results.Ok(new TestAutomationRuntimeSummaryDto(
            overall,
            DateTimeOffset.UtcNow,
            AppInfoEndpoints.Version,
            AppInfoEndpoints.BuildCode,
            passed,
            warnings,
            critical,
            checks));
    }

    private static void Add(
        List<TestAutomationRuntimeCheckDto> checks,
        string code,
        string title,
        string status,
        string severity,
        string message,
        string fixHint)
    {
        checks.Add(new TestAutomationRuntimeCheckDto(code, title, status, severity, message, fixHint));
    }
}

using Garmetix.Api.AppInfo;
using Garmetix.Api.Auth;
using Garmetix.Api.Testing;

namespace Garmetix.Api.Production;

public static class Stage10AFinalAcceptanceEndpoints
{
    private static readonly string[] RequiredManifestCodes =
    [
        "BACKEND_UNIT_TESTS",
        "FRONTEND_BUILD",
        "FRONTEND_ROUTE_ACCESS_AUDIT",
        "SECRET_HYGIENE_AUDIT",
        "DOCKER_ACCEPTANCE_DRILL",
        "BACKUP_RESTORE_SAFETY",
        "PERMISSION_ROLE_ACCEPTANCE",
        "PRINT_PDF_ACCEPTANCE",
        "SMTP_DELIVERY_ACCEPTANCE",
        "LICENSE_SAAS_ACTIVATION",
        "HR_EMPLOYEE_MASTER_ACCEPTANCE",
        "HR_BENEFITS_PAYROLL_ACCEPTANCE",
        "ATTENDANCE_CORE_ACCEPTANCE",
        "ATTENDANCE_STAGE9_FINAL_ACCEPTANCE",
        "TODAYS_DASHBOARD_ACCEPTANCE",
        "STAGE10A_FINAL_ACCEPTANCE",
        "STAGE10H_RUNTIME_DIAGNOSTICS",
        "AUTHENTICATED_API_SMOKE"
    ];

    public static RouteGroupBuilder MapStage10AFinalAcceptanceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/stage10a/final-acceptance")
            .WithTags("Stage 10A Final Acceptance")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("", Summary);
        group.MapGet("/checklist", Checklist);

        return group;
    }

    private static IResult Summary(ILoggerFactory loggerFactory)
    {
        try
        {
            var definitions = TestAutomationCatalog.BuildDefinitions();
            var definitionCodes = definitions.Select(item => item.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missing = RequiredManifestCodes.Where(code => !definitionCodes.Contains(code)).ToArray();
            var sections = BuildSections();
            var blocked = missing.Length > 0 || sections.Any(section => section.Required && section.Status == "Pending");

            return Results.Ok(CreatePayload(
                blocked ? "Needs attention" : "Ready for production acceptance",
                RequiredManifestCodes,
                missing,
                definitions.Count,
                sections,
                null));
        }
        catch (Exception ex)
        {
            loggerFactory.CreateLogger("Stage10AFinalAcceptance").LogError(ex, "Stage 10 final acceptance summary failed; returning safe degraded payload.");
            return Results.Ok(CreatePayload(
                "Degraded - review logs",
                RequiredManifestCodes,
                Array.Empty<string>(),
                0,
                BuildSections(),
                ex.Message));
        }
    }

    private static IResult Checklist(ILoggerFactory loggerFactory)
    {
        try
        {
            return Results.Ok(new
            {
                version = AppInfoEndpoints.Version,
                buildCode = AppInfoEndpoints.BuildCode,
                generatedAtUtc = DateTimeOffset.UtcNow,
                items = BuildSections().SelectMany(section => section.Items.Select(item => new
                {
                    section = section.Title,
                    item
                }))
            });
        }
        catch (Exception ex)
        {
            loggerFactory.CreateLogger("Stage10AFinalAcceptance").LogError(ex, "Stage 10 final acceptance checklist failed; returning safe degraded payload.");
            return Results.Ok(new
            {
                version = AppInfoEndpoints.Version,
                buildCode = AppInfoEndpoints.BuildCode,
                generatedAtUtc = DateTimeOffset.UtcNow,
                error = ex.Message,
                items = Array.Empty<object>()
            });
        }
    }

    private static object CreatePayload(
        string overallStatus,
        IReadOnlyList<string> requiredManifestCodes,
        IReadOnlyList<string> missingManifestCodes,
        int automatedCheckCount,
        IReadOnlyList<Stage10AFinalAcceptanceSection> sections,
        string? warning)
        => new
        {
            version = AppInfoEndpoints.Version,
            stage = AppInfoEndpoints.Stage,
            releaseName = AppInfoEndpoints.ReleaseName,
            buildCode = AppInfoEndpoints.BuildCode,
            generatedAtUtc = DateTimeOffset.UtcNow,
            overallStatus,
            requiredManifestCodes,
            missingManifestCodes,
            automatedCheckCount,
            sections,
            warning
        };

    private static Stage10AFinalAcceptanceSection[] BuildSections() =>
    [
        new("Build and deployment", "Review", true,
        [
            "Run docker compose production build successfully.",
            "Run frontend Nuxt build without TypeScript/Vite errors.",
            "Run backend dotnet test/publish on the deployment machine.",
            "Confirm /api/health and /api/app-info return current version/build."
        ]),
        new("Database and schema upgrade", "Review", true,
        [
            "Test fresh database install with Database:AutoMigrate enabled.",
            "Test existing PostgreSQL volume upgrade and startup schema repair.",
            "Run /api/database/repair once on a staging copy if any drift is reported.",
            "Verify HR, attendance, payroll and Today dashboard endpoints after upgrade."
        ]),
        new("Core business flows", "Review", true,
        [
            "Billing, purchase, vouchers, cash vouchers, petty cash and cash details open without errors.",
            "Store Operations opens first for store manager/biller users.",
            "Today's dashboard totals match sales, purchases, receipts, payments, expenses and attendance.",
            "Print/PDF acceptance can load sample document availability."
        ]),
        new("Attendance and payroll", "Review", true,
        [
            "Attendance Core, kiosk, photo proof review and final acceptance pages open.",
            "Attendance payroll review and salary draft generation remain explicit workflows.",
            "Salary slips and salary payments are generated only after explicit confirmation.",
            "No raw fingerprint image storage or real face recognition is enabled in Stage 10A."
        ]),
        new("Security and recovery", "Review", true,
        [
            "Secret hygiene check passes and no private .env file is packaged.",
            "License status and SMTP diagnostics pages load for admin users.",
            "Backup/restore drill succeeds on a disposable restore database.",
            "Role-wise permission final acceptance is reviewed before go-live."
        ])
    ];

    private sealed record Stage10AFinalAcceptanceSection(
        string Title,
        string Status,
        bool Required,
        IReadOnlyList<string> Items);
}

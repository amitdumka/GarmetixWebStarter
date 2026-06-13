using System.Diagnostics;
using System.Reflection;

namespace Garmetix.Api.AppInfo;

public static class AppInfoEndpoints
{
    public const string ProductName = "Garmetix";
    public const string Version = "4.0.0";
    public const string Stage = "Stage 8A";
    public const string ReleaseName = "UI Audit and Register Standardization";
    public const string BuildDate = "2026-06-13";
    public const string BuildCode = "GARMETIX-8A-20260613-4000";

    public static RouteGroupBuilder MapAppInfoEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/app-info")
            .WithTags("App Info")
            .AllowAnonymous();

        group.MapGet("", Info);
        group.MapGet("/version", VersionOnly);
        group.MapGet("/faq", Faq);
        group.MapGet("/contacts", Contacts);
        group.MapGet("/system", SystemInfo);

        return group;
    }

    private static IResult Info(IHostEnvironment environment)
        => Results.Ok(Create(environment.EnvironmentName));

    private static IResult VersionOnly(IHostEnvironment environment)
        => Results.Ok(new
        {
            productName = ProductName,
            version = Version,
            stage = Stage,
            releaseName = ReleaseName,
            buildDate = BuildDate,
            buildCode = BuildCode,
            environment = environment.EnvironmentName,
            assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        });


    private static IResult SystemInfo(IHostEnvironment environment)
    {
        var startedAt = Process.GetCurrentProcess().StartTime.ToUniversalTime();
        var now = DateTime.UtcNow;
        return Results.Ok(new AppSystemInfoDto(
            ProductName,
            Version,
            Stage,
            ReleaseName,
            BuildDate,
            BuildCode,
            environment.EnvironmentName,
            now.ToString("O"),
            startedAt.ToString("O"),
            Convert.ToInt64((now - startedAt).TotalSeconds),
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty));
    }

    private static IResult Faq() => Results.Ok(FaqItems);

    private static IResult Contacts() => Results.Ok(ContactItems);

    private static AppVersionDto Create(string environment)
        => new(
            ProductName,
            Version,
            Stage,
            ReleaseName,
            BuildDate,
            BuildCode,
            environment,
            Highlights,
            ContactItems,
            FaqItems);

    private static readonly string[] Highlights =
    [
        "The UI audit is now an actionable page queue with persistent status, notes, filters and direct navigation.",
        "Shared register states provide consistent loading, error, retry and empty experiences.",
        "Credit Note, Debit Note, Commercial Note and Customer registers use the Stage 8 responsive layout.",
        "Primary create actions remain visible when pages also provide secondary header actions.",
        "Accounting and customer tables use searchable registers, aligned values and responsive action groups.",
        "Frontend, backend, npm package and .NET assembly versions are synchronized for every release."
    ];

    private static readonly AppContactDto[] ContactItems =
    [
        new("Business owner", "Amit Kumar", "person", "Primary owner/contact for deployment decisions."),
        new("Email", "ameetkrsah@gmail.com", "email", "Use for account, deployment and support communication."),
        new("Location", "Dumka / Jamshedpur, Jharkhand, India", "location", "Business operating region."),
        new("Product", "Garmetix", "product", "Garment store management, billing, purchase, inventory, GST and SaaS admin.")
    ];

    private static readonly AppFaqDto[] FaqItems =
    [
        new("How do I know which code version is running?", "Open About Us and check the Version, Stage, Build Date and Build Code. The same data is also available from /api/app-info/version.", "Version"),
        new("When should the version number change?", "Every packaged code change should update the central version constants in backend AppInfoEndpoints and frontend appVersion.ts so the UI and API identify the same release.", "Version"),
        new("Where do I onboard the first company?", "Use Admin > Onboarding. It creates company, store group, store, users, employees, manager salesman and optional basic masters.", "Onboarding"),
        new("Where do I seed AF/SS default data?", "Use Admin > AF/SS, select the target company/profile, then run the default data seed.", "Seeder"),
        new("Where can I see success or failure messages?", "Use Admin > Message Logs. You can filter by source, level, success/failure, search text and date range.", "Logs"),
        new("How do I edit my own profile?", "Use Account > My Profile. You can change your name, username, email and password. Role, permissions and workspace assignment remain admin-controlled.", "Profile"),
        new("Where do I record legally Non-GST goods?", "Use Operations > Non-GST Goods. These documents are not hidden; they are recorded in a separate register, posted to visible accounting ledgers and excluded from GST reports because tax rate is zero/out-of-scope.", "Non-GST Goods"),
        new("What should I run after extracting a new package?", "Run dotnet build, npm ci, npm run build, and docker compose up --build from the extracted project.", "Deployment")
    ];
}

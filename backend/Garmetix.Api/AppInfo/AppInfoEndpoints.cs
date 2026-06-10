using System.Reflection;

namespace Garmetix.Api.AppInfo;

public static class AppInfoEndpoints
{
    public const string ProductName = "Garmetix";
    public const string Version = "3.1.0";
    public const string Stage = "Stage 7B";
    public const string ReleaseName = "Dashboard Analytics, Role Routing and Template Alignment";
    public const string BuildDate = "2026-06-10";
    public const string BuildCode = "GARMETIX-7B-20260610-310";

    public static RouteGroupBuilder MapAppInfoEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/app-info")
            .WithTags("App Info")
            .AllowAnonymous();

        group.MapGet("", Info);
        group.MapGet("/version", VersionOnly);
        group.MapGet("/faq", Faq);
        group.MapGet("/contacts", Contacts);

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
        "Stage 7B role-aware /dashboard landing redirects users to the correct dashboard",
        "Dashboard shell now adds a smart dashboard menu item and topbar shortcut",
        "Store manager dashboard now includes quick actions and daily health signals",
        "Business dashboard now includes executive actions, health signals and store-group performance",
        "Dashboard backend adds /api/dashboard/home and deeper role/workspace scoped KPIs",
        "Nuxt UI dashboard-template style shell with collapsible sidebar, topbar, command search and dark-mode polish",
        "Store Manager dashboard scoped to the logged-in user current store",
        "Owner/Admin/Accountant dashboard with company, store-group and store-wise KPIs",
        "All existing pages and menus preserved; legacy shell can be restored with NUXT_PUBLIC_DASHBOARD_SHELL=legacy",
        "Stage 7 TODO and implementation map added for safe two-part dashboard migration",
        "Modern login screen with one primary Login button and compact forgot/reset links",
        "Global auth guard redirects expired or missing sessions to Login before protected pages render",
        "API 401 handling now clears expired sessions and returns the user to Login",
        "My Profile page with editable name, username, email and self-service password change",
        "Profile changes never allow users to modify their own role, permission or workspace assignment",
        "Company, store group and store onboarding module",
        "AF/SS default data seeder module",
        "Application message logs with filters",
        "Version identity surfaced in About Us and API",
        "Non-GST/out-of-scope goods purchase and sale module",
        "Buildfix: added missing Store namespace import for Docker publish",
        "Compile fix: resolved stock variable shadowing and ProductCategory ambiguity",
        "Multi-item Non-GST purchase memo and sale cash memo with print view",
        "Non-GST sale, purchase, profit and current stock reports",
        "Separate Non-GST stock flag and movement ledger",
        "Separate Non-GST goods reports and visible accounting postings",
        "About Us, Contact Us and FAQ pages",
        "Runtime fix: removed duplicate app-info and message-log root routes",
        "Runtime fix: Message Logs PostgreSQL filter query no longer uses untyped NULL parameters",
        "Runtime fix: Non-GST sale sends null stock id instead of blank string and purchase auto-generates barcode when blank"
    ];

    private static readonly AppContactDto[] ContactItems =
    [
        new("Business owner", "Amit Kumar", "person", "Primary owner/contact for deployment decisions."),
        new("Email", "ameetkrsah@gmail.com", "email", "Use for account, deployment and support communication."),
        new("Location", "Dumka / Jamshedpur, Jharkhand, India", "location", "Business operating region."),
        new("Product", "Garmetix Web", "product", "Garment store management, billing, purchase, inventory, GST and SaaS admin.")
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

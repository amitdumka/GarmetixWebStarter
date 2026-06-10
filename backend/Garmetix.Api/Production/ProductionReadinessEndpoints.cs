using Garmetix.Api.Auth;
using Garmetix.Api.Backup;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace Garmetix.Api.Production;

public static class ProductionReadinessEndpoints
{
    private static readonly string[] WeakSecretMarkers =
    [
        "change-this",
        "development",
        "garmetix_dev",
        "password",
        "secret"
    ];

    public static RouteGroupBuilder MapProductionReadinessEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/production-readiness")
            .WithTags("Production Readiness")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/summary", BuildSummaryAsync);
        group.MapGet("/checklist", () => Results.Ok(new
        {
            items = new[]
            {
                "Use docker-compose.prod.yml with a copied .env.production file.",
                "Set strong POSTGRES_PASSWORD and JWT_SIGNING_KEY values.",
                "Set CORS_ALLOWED_ORIGINS to the exact HTTPS site origin.",
                "Run scripts/linux/production-preflight.sh before first live use.",
                "Configure HTTPS through Caddy, Nginx, Cloudflare Tunnel, or another reverse proxy.",
                "Verify backup, restore preflight, and off-site backup before opening billing to users.",
                "Run /data-consistency and repair only reviewed issues before going live.",
                "Keep update and rollback scripts beside every deployed release archive."
            }
        }));

        return group;
    }

    private static async Task<IResult> BuildSummaryAsync(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IOptions<BackupOptions> backupOptions,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        var checks = new List<ProductionReadinessCheckDto>();
        AddEnvironmentCheck(checks, environment);
        AddJwtCheck(checks, configuration);
        AddDatabaseSecretCheck(checks, configuration);
        AddCorsCheck(checks, configuration, environment);
        AddUrlCheck(checks, configuration, environment);
        AddEmailCheck(checks, configuration, environment);
        AddBackupCheck(checks, backupOptions.Value);
        AddGoogleDriveCheck(checks, configuration);
        AddOracleSyncCheck(checks, configuration);
        AddForwardedHeadersCheck(checks, configuration, environment);
        AddLoggingCheck(checks, configuration);

        var critical = checks.Count(item => IsCritical(item));
        var warnings = checks.Count(item => string.Equals(item.Status, "Warning", StringComparison.OrdinalIgnoreCase));
        var passed = checks.Count(item => string.Equals(item.Status, "Pass", StringComparison.OrdinalIgnoreCase));
        var status = critical > 0
            ? "Blocked"
            : warnings > 0 ? "Needs attention" : "Ready";

        return Results.Ok(new ProductionReadinessSummaryDto(
            status,
            environment.EnvironmentName,
            DateTimeOffset.UtcNow,
            passed,
            warnings,
            critical,
            checks));
    }

    private static void AddEnvironmentCheck(List<ProductionReadinessCheckDto> checks, IWebHostEnvironment environment)
    {
        Add(checks,
            "ENVIRONMENT",
            "ASP.NET Core environment",
            environment.IsProduction() ? "Pass" : "Warning",
            environment.IsProduction() ? "Info" : "Medium",
            environment.IsProduction()
                ? "API is running with ASPNETCORE_ENVIRONMENT=Production."
                : $"API is running as {environment.EnvironmentName}. Use Production for live deployment.",
            "Set ASPNETCORE_ENVIRONMENT=Production in docker-compose.prod.yml or the service environment.");
    }

    private static void AddJwtCheck(List<ProductionReadinessCheckDto> checks, IConfiguration configuration)
    {
        var signingKey = configuration["Jwt:SigningKey"] ?? string.Empty;
        var weak = signingKey.Length < 32 || ContainsWeakMarker(signingKey);
        Add(checks,
            "JWT_SECRET",
            "JWT signing key strength",
            weak ? "Critical" : "Pass",
            weak ? "High" : "Info",
            weak
                ? "JWT signing key is missing, too short, or still looks like a development/default value."
                : "JWT signing key length and pattern look acceptable.",
            "Generate at least 48 random characters and set JWT_SIGNING_KEY in .env.production.");
    }

    private static void AddDatabaseSecretCheck(List<ProductionReadinessCheckDto> checks, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default") ?? string.Empty;
        var weakPassword = Regex.IsMatch(connectionString, "Password\\s*=\\s*(garmetix_dev|password|change-this|postgres)", RegexOptions.IgnoreCase);
        var hasPostgres = connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase)
            && connectionString.Contains("Database=", StringComparison.OrdinalIgnoreCase);

        Add(checks,
            "DB_SECRET",
            "Database connection string",
            !hasPostgres || weakPassword ? "Critical" : "Pass",
            !hasPostgres || weakPassword ? "High" : "Info",
            !hasPostgres
                ? "Default database connection string is missing or incomplete."
                : weakPassword ? "Database password still looks like a default/development value." : "Database connection string is configured and does not expose an obvious default password.",
            "Set POSTGRES_PASSWORD to a strong value and never commit the real .env.production file.");
    }

    private static void AddCorsCheck(List<ProductionReadinessCheckDto> checks, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var origins = GetConfiguredOrigins(configuration);
        var onlyLocalhost = origins.Length == 0 || origins.All(IsLocalhostOrigin);
        Add(checks,
            "CORS_ORIGINS",
            "Allowed browser origins",
            environment.IsProduction() && onlyLocalhost ? "Warning" : "Pass",
            environment.IsProduction() && onlyLocalhost ? "Medium" : "Info",
            origins.Length == 0
                ? "No explicit Cors:AllowedOrigins configured; API falls back to local development origin."
                : $"Configured origins: {string.Join(", ", origins)}",
            "Set CORS_ALLOWED_ORIGINS=https://your-domain.example in production.");
    }

    private static void AddUrlCheck(List<ProductionReadinessCheckDto> checks, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var resetUrl = configuration["PasswordReset:FrontendBaseUrl"] ?? string.Empty;
        var usesHttps = resetUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        var local = resetUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase);
        Add(checks,
            "PUBLIC_URL",
            "Public frontend URL",
            environment.IsProduction() && (!usesHttps || local) ? "Warning" : "Pass",
            environment.IsProduction() && (!usesHttps || local) ? "Medium" : "Info",
            string.IsNullOrWhiteSpace(resetUrl)
                ? "Password reset frontend URL is empty."
                : $"Password reset frontend URL is set to {MaskUrl(resetUrl)}.",
            "Use the public HTTPS URL in PASSWORD_RESET_FRONTEND_BASE_URL.");
    }

    private static void AddEmailCheck(List<ProductionReadinessCheckDto> checks, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var enabled = configuration.GetValue<bool>("Email:Enabled");
        var host = configuration["Email:Host"] ?? string.Empty;
        var fromEmail = configuration["Email:FromEmail"] ?? string.Empty;
        var ok = enabled && !string.IsNullOrWhiteSpace(host) && !host.Contains("example.com", StringComparison.OrdinalIgnoreCase) && fromEmail.Contains('@');
        Add(checks,
            "EMAIL_SMTP",
            "SMTP email configuration",
            environment.IsProduction() && !ok ? "Warning" : "Pass",
            environment.IsProduction() && !ok ? "Medium" : "Info",
            ok ? "SMTP email appears configured for password reset and alerts." : "SMTP email is disabled or still uses placeholder values.",
            "Set EMAIL_ENABLED=true and configure SMTP host, username, password, and from address.");
    }

    private static void AddBackupCheck(List<ProductionReadinessCheckDto> checks, BackupOptions backup)
    {
        var enabled = backup.Enabled;
        var hasDirectory = !string.IsNullOrWhiteSpace(backup.Directory);
        var retentionOk = backup.RetentionCount >= 7;
        var directoryReady = false;
        try
        {
            if (hasDirectory)
            {
                Directory.CreateDirectory(backup.Directory);
                directoryReady = Directory.Exists(backup.Directory);
            }
        }
        catch
        {
            directoryReady = false;
        }

        Add(checks,
            "BACKUP_LOCAL",
            "Local database backups",
            enabled && hasDirectory && retentionOk && directoryReady ? "Pass" : "Critical",
            enabled && hasDirectory && retentionOk && directoryReady ? "Info" : "High",
            enabled && hasDirectory && retentionOk && directoryReady
                ? $"Local backups are enabled with retention count {backup.RetentionCount}."
                : "Local backups are disabled, missing a usable directory, or retention is too low.",
            "Keep BACKUP_ENABLED=true, mount ./backups, and keep at least 7 restore points.");
    }

    private static void AddGoogleDriveCheck(List<ProductionReadinessCheckDto> checks, IConfiguration configuration)
    {
        var enabled = configuration.GetValue<bool>("GoogleDriveBackup:Enabled");
        var folderId = configuration["GoogleDriveBackup:FolderId"] ?? string.Empty;
        var jsonPath = configuration["GoogleDriveBackup:ServiceAccountJsonPath"] ?? string.Empty;
        var inlineJson = configuration["GoogleDriveBackup:ServiceAccountJson"] ?? string.Empty;
        var configured = !enabled || (!string.IsNullOrWhiteSpace(folderId) && (!string.IsNullOrWhiteSpace(inlineJson) || File.Exists(jsonPath)));
        Add(checks,
            "BACKUP_OFFSITE",
            "Off-site/cloud backup",
            configured && enabled ? "Pass" : enabled ? "Warning" : "Warning",
            enabled && !configured ? "Medium" : "Low",
            enabled
                ? configured ? "Google Drive backup appears configured." : "Google Drive backup is enabled but folder/service-account settings are incomplete."
                : "Off-site backup is disabled. Local backup alone is not enough for production.",
            "Enable GOOGLE_DRIVE_BACKUP_ENABLED and share the Drive folder with the service account, or configure another off-site backup process.");
    }

    private static void AddOracleSyncCheck(List<ProductionReadinessCheckDto> checks, IConfiguration configuration)
    {
        var autoApply = configuration.GetValue<bool>("OracleSync:ApplyInboundAutomatically");
        var requireTrustedSource = configuration.GetValue<bool>("OracleSync:RequireTrustedSourceForAutoApply");
        var trustedSources = configuration["OracleSync:TrustedSourceApplicationsCsv"] ?? string.Empty;
        var safe = !autoApply || (requireTrustedSource && !string.IsNullOrWhiteSpace(trustedSources));
        Add(checks,
            "ORACLE_AUTO_APPLY",
            "Oracle inbound auto-apply safety",
            safe ? "Pass" : "Critical",
            safe ? "Info" : "High",
            safe
                ? "Oracle inbound auto-apply is disabled or guarded by trusted source filtering."
                : "Oracle inbound auto-apply is enabled without trusted source filtering.",
            "Keep ORACLE_SYNC_APPLY_INBOUND_AUTOMATICALLY=false until trusted sources and ownership rules are approved.");
    }

    private static void AddForwardedHeadersCheck(List<ProductionReadinessCheckDto> checks, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var enabled = string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_FORWARDEDHEADERS_ENABLED"), "true", StringComparison.OrdinalIgnoreCase)
            || configuration.GetValue<bool>("ForwardedHeaders:Enabled");
        Add(checks,
            "REVERSE_PROXY",
            "Reverse proxy forwarded headers",
            environment.IsProduction() && !enabled ? "Warning" : "Pass",
            environment.IsProduction() && !enabled ? "Medium" : "Info",
            enabled
                ? "Forwarded header support is enabled for reverse proxy/tunnel deployments."
                : "Forwarded headers are not enabled; HTTPS redirects and generated links may be wrong behind a proxy.",
            "Set ASPNETCORE_FORWARDEDHEADERS_ENABLED=true when running behind Caddy, Nginx, or Cloudflare Tunnel.");
    }

    private static void AddLoggingCheck(List<ProductionReadinessCheckDto> checks, IConfiguration configuration)
    {
        var defaultLevel = configuration["Logging:LogLevel:Default"] ?? "Information";
        var aspNetLevel = configuration["Logging:LogLevel:Microsoft.AspNetCore"] ?? string.Empty;
        var ok = !string.Equals(defaultLevel, "Debug", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(defaultLevel, "Trace", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(aspNetLevel, "Debug", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(aspNetLevel, "Trace", StringComparison.OrdinalIgnoreCase);
        Add(checks,
            "LOG_LEVEL",
            "Production log level",
            ok ? "Pass" : "Warning",
            ok ? "Info" : "Low",
            ok ? $"Default log level is {defaultLevel}." : "Verbose Debug/Trace logging is enabled.",
            "Use Information or Warning for production and use docker logs/journald retention.");
    }

    private static void Add(List<ProductionReadinessCheckDto> checks, string code, string title, string status, string severity, string message, string fixHint)
    {
        checks.Add(new ProductionReadinessCheckDto(code, title, status, severity, message, fixHint));
    }

    private static bool ContainsWeakMarker(string value) => WeakSecretMarkers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));

    private static bool IsCritical(ProductionReadinessCheckDto item) => string.Equals(item.Status, "Critical", StringComparison.OrdinalIgnoreCase);

    private static string[] GetConfiguredOrigins(IConfiguration configuration)
    {
        var fromArray = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        var fromCsv = (configuration["Cors:AllowedOriginsCsv"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return fromArray.Concat(fromCsv).Where(origin => !string.IsNullOrWhiteSpace(origin)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool IsLocalhostOrigin(string origin) => origin.Contains("localhost", StringComparison.OrdinalIgnoreCase)
        || origin.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || origin.Contains("0.0.0.0", StringComparison.OrdinalIgnoreCase);

    private static string MaskUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url;
        }

        return $"{uri.Scheme}://{uri.Host}";
    }
}

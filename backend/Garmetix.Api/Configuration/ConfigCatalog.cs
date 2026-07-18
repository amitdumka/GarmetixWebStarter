using Garmetix.Core.Models.Configuration;

namespace Garmetix.Api.Configuration;

public sealed record ConfigDefinition(
    string Key,
    string Category,
    string DisplayName,
    string? Description,
    bool IsSecret,
    string? EnvVarName,
    bool WriteToApiEnv);

/// <summary>
/// The known configuration surface this module manages - covers the runtime settings the shared
/// API actually reads today from /etc/garmetix/srp-api.env (Database, JWT, Email, License,
/// GSTIN, Oracle Sync, Backup, Payroll Automation, CORS, WhatsApp, Digital Bills, Assistant/AI,
/// Communication, DotMatrix), plus the Cloudflare/deploy-tooling keys that used to live in the
/// legacy ubuntu.env/.env.production files. Cloudflare entries are deliberately WriteToApiEnv:
/// false - the .NET API never reads them (they're consumed by shell scripts and the Cloudflare
/// API directly) - they're stored here securely for central reference, not auto-applied by the
/// restart action, so this catalog never overpromises what a "write to disk" actually changes.
/// </summary>
public static class ConfigCatalog
{
    public static readonly IReadOnlyList<ConfigDefinition> Definitions = new List<ConfigDefinition>
    {
        // Database
        new("ConnectionStrings__Default", "Database", "Primary Connection String", "The main garmetix_srp Postgres connection string.", true, "ConnectionStrings__Default", true),

        // JWT
        new("Jwt__SigningKey", "Authentication", "JWT Signing Key", "Signs every login token - changing this logs everyone out.", true, "Jwt__SigningKey", true),
        new("Jwt__Issuer", "Authentication", "JWT Issuer", null, false, "Jwt__Issuer", true),
        new("Jwt__Audience", "Authentication", "JWT Audience", null, false, "Jwt__Audience", true),

        // Email (SMTP - legacy forgot-password sender, distinct from Communication & Mail's provider registry)
        new("Email__Enabled", "Email (Legacy SMTP)", "Enabled", "Password-reset email sender - separate from Communication & Mail's provider system.", false, "Email__Enabled", true),
        new("Email__Host", "Email (Legacy SMTP)", "SMTP Host", null, false, "Email__Host", true),
        new("Email__Port", "Email (Legacy SMTP)", "SMTP Port", null, false, "Email__Port", true),
        new("Email__EnableSsl", "Email (Legacy SMTP)", "Enable SSL", null, false, "Email__EnableSsl", true),
        new("Email__UserName", "Email (Legacy SMTP)", "SMTP Username", null, false, "Email__UserName", true),
        new("Email__Password", "Email (Legacy SMTP)", "SMTP Password", null, true, "Email__Password", true),
        new("Email__FromEmail", "Email (Legacy SMTP)", "From Email", null, false, "Email__FromEmail", true),
        new("Email__FromName", "Email (Legacy SMTP)", "From Name", null, false, "Email__FromName", true),

        // License
        new("License__EnforcementEnabled", "License", "Enforcement Enabled", null, false, "License__EnforcementEnabled", true),
        new("License__MasterSecret", "License", "Master Secret", "Signs license activation files.", true, "License__MasterSecret", true),
        new("License__ProductCode", "License", "Product Code", null, false, "License__ProductCode", true),
        new("License__DefaultPlan", "License", "Default Plan", null, false, "License__DefaultPlan", true),
        new("License__DefaultMaxStores", "License", "Default Max Stores", null, false, "License__DefaultMaxStores", true),
        new("License__DefaultMaxUsers", "License", "Default Max Users", null, false, "License__DefaultMaxUsers", true),

        // GSTIN Lookup
        new("GstinLookup__Enabled", "GSTIN Lookup", "Enabled", null, false, "GstinLookup__Enabled", true),
        new("GstinLookup__BaseUrl", "GSTIN Lookup", "Base URL", null, false, "GstinLookup__BaseUrl", true),
        new("GstinLookup__ApiKey", "GSTIN Lookup", "API Key", null, true, "GstinLookup__ApiKey", true),

        // Oracle Sync
        new("OracleSync__Enabled", "Oracle Sync", "Enabled", null, false, "OracleSync__Enabled", true),
        new("OracleSync__ConnectionString", "Oracle Sync", "Connection String", null, true, "OracleSync__ConnectionString", true),
        new("OracleSync__TenantId", "Oracle Sync", "Tenant Id", null, false, "OracleSync__TenantId", true),

        // Backup
        new("Backup__Enabled", "Backup", "Enabled", null, false, "Backup__Enabled", true),
        new("Backup__RetentionCount", "Backup", "Retention Count", null, false, "Backup__RetentionCount", true),
        new("Backup__RunHour", "Backup", "Run Hour (IST)", null, false, "Backup__RunHour", true),

        // Google Drive Backup
        new("GoogleDriveBackup__Enabled", "Google Drive Backup", "Enabled", null, false, "GoogleDriveBackup__Enabled", true),
        new("GoogleDriveBackup__ServiceAccountJson", "Google Drive Backup", "Service Account JSON", null, true, "GoogleDriveBackup__ServiceAccountJson", true),
        new("GoogleDriveBackup__FolderId", "Google Drive Backup", "Folder Id", null, false, "GoogleDriveBackup__FolderId", true),

        // Payroll Automation
        new("PayrollAutomation__Enabled", "Payroll Automation", "Enabled", null, false, "PayrollAutomation__Enabled", true),
        new("PayrollAutomation__RunHour", "Payroll Automation", "Run Hour (IST)", null, false, "PayrollAutomation__RunHour", true),

        // CORS
        new("Cors__AllowedOriginsCsv", "Network", "Allowed CORS Origins (CSV)", null, false, "Cors__AllowedOriginsCsv", true),

        // WhatsApp
        new("WhatsApp__MetaCloudApiBaseUrl", "WhatsApp", "Meta Cloud API Base URL", null, false, "WhatsApp__MetaCloudApiBaseUrl", true),
        new("WhatsApp__MetaWebhookVerifyToken", "WhatsApp", "Webhook Verify Token", null, true, "WhatsApp__MetaWebhookVerifyToken", true),

        // Digital Bills
        new("DigitalBills__PublicBaseUrl", "Digital Bills", "Public Base URL", null, false, "DigitalBills__PublicBaseUrl", true),

        // Assistant / AI
        new("Assistant__Enabled", "Assistant (AI)", "Enabled", null, false, "Assistant__Enabled", true),
        new("Assistant__Provider", "Assistant (AI)", "Provider", "anthropic or gemini", false, "Assistant__Provider", true),
        new("Assistant__AnthropicApiKey", "Assistant (AI)", "Anthropic API Key", null, true, "Assistant__AnthropicApiKey", true),
        new("Gemini__Enabled", "Assistant (AI)", "Gemini Enabled", null, false, "Gemini__Enabled", true),
        new("Gemini__ApiKey", "Assistant (AI)", "Gemini API Key", null, true, "Gemini__ApiKey", true),

        // Communication & Mail webhook
        new("Communication__BrevoWebhook__Enabled", "Communication & Mail", "Brevo Webhook Enabled", null, false, "Communication__BrevoWebhook__Enabled", true),
        new("Communication__BrevoWebhook__BasicAuthUsername", "Communication & Mail", "Webhook Basic Auth Username", null, false, "Communication__BrevoWebhook__BasicAuthUsername", true),
        new("Communication__BrevoWebhook__BasicAuthPassword", "Communication & Mail", "Webhook Basic Auth Password", null, true, "Communication__BrevoWebhook__BasicAuthPassword", true),

        // Dot Matrix Printing
        new("DotMatrixPrinting__Enabled", "Dot Matrix Printing", "Enabled", null, false, "DotMatrixPrinting__Enabled", true),
        new("DotMatrixPrinting__PrinterName", "Dot Matrix Printing", "Printer Name", null, false, "DotMatrixPrinting__PrinterName", true),

        // Cloudflare - reference only. The API never reads these; deploy tooling and the
        // Cloudflare API do. Stored here for secure central reference, not auto-applied.
        new("CLOUDFLARE_API_TOKEN", "Cloudflare (Reference Only)", "API Token", "Not read by the API - deploy tooling only. Stored here for secure reference.", true, null, false),
        new("CLOUDFLARE_ACCOUNT_ID", "Cloudflare (Reference Only)", "Account Id", null, false, null, false),
        new("CLOUDFLARE_ZONE_ID", "Cloudflare (Reference Only)", "Zone Id", null, false, null, false),
        new("CLOUDFLARE_TUNNEL_NAME", "Cloudflare (Reference Only)", "Tunnel Name", null, false, null, false),
        new("CLOUDFLARE_TUNNEL_TOKEN", "Cloudflare (Reference Only)", "Tunnel Token", "Not read by the API - the cloudflared systemd service reads its own copy.", true, null, false),
    };

    public static ConfigDefinition? Find(string key) => Definitions.FirstOrDefault(d => d.Key == key);
}

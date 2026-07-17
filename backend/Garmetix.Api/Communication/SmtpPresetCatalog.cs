using Garmetix.Core.Models.Communication;

namespace Garmetix.Api.Communication;

public sealed record SmtpPresetDefaults(string Host, int Port, bool EnableSsl, bool UseStartTls, string Description);

/// <summary>
/// SMTP presets supply default host/port/TLS values only - SmtpEmailProviderClient is the
/// single implementation that serves every preset. A provider's own Host/Port/EnableSsl/
/// UseStartTls always win once set; presets only seed the create-provider form and document
/// the expected shape.
/// </summary>
public static class SmtpPresetCatalog
{
    public static readonly IReadOnlyDictionary<string, SmtpPresetDefaults> Presets = new Dictionary<string, SmtpPresetDefaults>
    {
        [EmailCatalog.SmtpPresets.Brevo] = new("smtp-relay.brevo.com", 587, true, true, "Brevo SMTP relay (login = Brevo account email, password = SMTP key from Brevo settings)."),
        [EmailCatalog.SmtpPresets.GoDaddyProfessionalEmail] = new("smtpout.secureserver.net", 587, true, true, "GoDaddy Professional Email / Microsoft-hosted GoDaddy mailbox."),
        [EmailCatalog.SmtpPresets.Microsoft365] = new("smtp.office365.com", 587, true, true, "Microsoft 365 / Exchange Online (requires SMTP AUTH enabled on the mailbox)."),
        [EmailCatalog.SmtpPresets.Gmail] = new("smtp.gmail.com", 587, true, true, "Gmail/Google Workspace (requires an App Password, not the account password)."),
        [EmailCatalog.SmtpPresets.Custom] = new(string.Empty, 587, true, true, "Custom SMTP host - fill in Host/Port/credentials manually."),
        [EmailCatalog.SmtpPresets.LocalPostfixRelay] = new("localhost", 25, false, false, "Local Postfix relay (see CM-10, isolated/disabled-by-default deployment)."),
    };

    public static SmtpPresetDefaults? TryGet(string? presetKey) =>
        !string.IsNullOrWhiteSpace(presetKey) && Presets.TryGetValue(presetKey, out var defaults) ? defaults : null;
}

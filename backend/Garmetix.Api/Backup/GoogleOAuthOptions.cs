namespace Garmetix.Api.Backup;

/// <summary>
/// The Garmetix platform's own Google Cloud OAuth 2.0 Web application client - one OAuth app shared
/// by every company, each of whom grants their own consent to their own Drive (standard OAuth model:
/// one client identity, many end-users). Create this once in Google Cloud Console (APIs & Services ->
/// Credentials -> OAuth client ID -> Web application), with RedirectUri added to its Authorized
/// redirect URIs list exactly as configured here. All empty by default - every endpoint that needs
/// this reports an honest "not configured yet" rather than pretending to work.
/// </summary>
public sealed class GoogleOAuthOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Must exactly match an "Authorized redirect URI" on the Google Cloud OAuth client, e.g. https://srp.aadwikafashion.in/api/backups/drive/oauth/callback.</summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>Where to send the browser back to after the OAuth callback finishes, e.g. https://srp.aadwikafashion.in/admin/google-drive-backup.</summary>
    public string FrontendReturnUrl { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret)
        && !string.IsNullOrWhiteSpace(RedirectUri);
}

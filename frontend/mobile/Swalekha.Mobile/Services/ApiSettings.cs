namespace Swalekha.Mobile.Services;

/// <summary>
/// The API host is the shared Garmetix.Api process (the same backend every other Garmetix app
/// talks to) - Swalekha's endpoints live under /api/swalekha on that same host. Overridable at
/// runtime (Preferences-backed) so a build can be pointed at a dev/staging host without a rebuild,
/// since this app cannot be live-tested against the production SRP host from a build environment.
/// </summary>
public static class ApiSettings
{
    private const string PreferenceKey = "swalekha.api.base_url";
    private const string DefaultBaseUrl = "https://srp.aadwikafashion.in/";

    public static string BaseUrl
    {
        get => Preferences.Default.Get(PreferenceKey, DefaultBaseUrl);
        set => Preferences.Default.Set(PreferenceKey, NormalizeBaseUrl(value));
    }

    private static string NormalizeBaseUrl(string value)
    {
        var trimmed = string.IsNullOrWhiteSpace(value) ? DefaultBaseUrl : value.Trim();
        return trimmed.EndsWith('/') ? trimmed : trimmed + "/";
    }
}

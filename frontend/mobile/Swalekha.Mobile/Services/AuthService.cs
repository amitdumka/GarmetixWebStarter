using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Swalekha.Mobile.Models;

namespace Swalekha.Mobile.Services;

/// <summary>
/// Swalekha's own backend policy (GarmetixPolicies.SwalekhaOwner) already rejects any non-Owner
/// JWT on every /api/swalekha/* call, so this is defense in depth, not the real gate - but
/// checking UserType right at login gives a clear "wrong account type" message instead of the
/// user reaching the dashboard and having every request fail with 403.
/// </summary>
public sealed class AuthService
{
    private const string SessionStorageKey = "swalekha.session";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _anonymousClient;

    public AuthService(IHttpClientFactory httpClientFactory)
    {
        // Deliberately the plain "anonymous" named client (no auth handler) - login itself
        // must not attach a stale/absent bearer token.
        _anonymousClient = httpClientFactory.CreateClient("SwalekhaAnonymous");
    }

    public AuthUserDto? CurrentUser { get; private set; }

    public string? CurrentToken { get; private set; }

    public bool IsAuthenticated => CurrentToken is not null && CurrentUser is not null;

    public async Task<AuthUserDto> LoginAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await _anonymousClient.PostAsJsonAsync(
                "api/auth/login",
                new LoginRequest(userName, password),
                JsonOptions,
                cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new SwalekhaApiException($"Couldn't reach the server at {ApiSettings.BaseUrl}. Check the address and your connection.");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new SwalekhaApiException("Incorrect username or password.", 401);
        }

        if (!response.IsSuccessStatusCode)
        {
            var detail = await TryReadMessageAsync(response, cancellationToken);
            throw new SwalekhaApiException(detail ?? $"Sign-in failed ({(int)response.StatusCode}).", (int)response.StatusCode);
        }

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions, cancellationToken)
            ?? throw new SwalekhaApiException("The server returned an empty sign-in response.");

        if (!string.Equals(auth.User.UserType, "Owner", StringComparison.OrdinalIgnoreCase))
        {
            throw new SwalekhaApiException("Swalekha is for Owner accounts only. Sign in with the Owner login.");
        }

        if (!auth.User.IsActive)
        {
            throw new SwalekhaApiException("This account is inactive. Contact an Owner or Admin.");
        }

        CurrentToken = auth.Token;
        CurrentUser = auth.User;
        await PersistSessionAsync(auth, cancellationToken);

        return auth.User;
    }

    public async Task<bool> RestoreSessionAsync()
    {
        string? raw;
        try
        {
            raw = await SecureStorage.Default.GetAsync(SessionStorageKey);
        }
        catch (Exception)
        {
            // SecureStorage can throw if the platform keystore was reset (e.g. app data cleared
            // outside the app) - treat that the same as "no saved session".
            return false;
        }

        if (string.IsNullOrEmpty(raw))
        {
            return false;
        }

        var auth = JsonSerializer.Deserialize<AuthResponse>(raw, JsonOptions);
        if (auth is null || auth.ExpiresAtUtc <= DateTime.UtcNow)
        {
            SecureStorage.Default.Remove(SessionStorageKey);
            return false;
        }

        CurrentToken = auth.Token;
        CurrentUser = auth.User;
        return true;
    }

    public void Logout()
    {
        CurrentToken = null;
        CurrentUser = null;
        SecureStorage.Default.Remove(SessionStorageKey);
    }

    private static async Task PersistSessionAsync(AuthResponse auth, CancellationToken cancellationToken)
    {
        var raw = JsonSerializer.Serialize(auth, JsonOptions);
        await SecureStorage.Default.SetAsync(SessionStorageKey, raw);
        _ = cancellationToken;
    }

    private static async Task<string?> TryReadMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return doc.RootElement.TryGetProperty("message", out var message) ? message.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}

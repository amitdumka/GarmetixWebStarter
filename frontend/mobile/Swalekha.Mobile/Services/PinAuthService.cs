using System.Security.Cryptography;
using System.Text;

namespace Swalekha.Mobile.Services;

/// <summary>
/// Backend JWTs expire 10 hours after login (JwtTokenService.cs) and this backend has no
/// refresh-token endpoint - the only way to get a new token is a full username+password
/// POST /api/auth/login. Retyping that every few hours is the exact friction Amit asked to
/// remove: a 4-digit PIN gates access to this device's copy of the app, and on unlock either
/// resumes the still-valid session or silently re-runs a real login with the vaulted
/// credentials if it expired - "auto login" from the user's point of view.
///
/// Trade-off, stated plainly: this means the actual account password is stored on-device
/// (via SecureStorage - Android Keystore-backed encryption, not plaintext prefs) rather than
/// a short-lived, server-revocable refresh token, because no refresh token exists to store
/// instead. A backend refresh-token endpoint would be the more conventional long-term fix;
/// this is the client-only option available today, and only ever active if the Owner
/// explicitly opts in via "Set up quick access" - never on by default.
/// </summary>
public sealed class PinAuthService
{
    private const string PinHashKey = "swalekha.pin.hash";
    private const string PinSaltKey = "swalekha.pin.salt";
    private const string VaultUserNameKey = "swalekha.pin.vault.username";
    private const string VaultPasswordKey = "swalekha.pin.vault.password";

    public async Task<bool> HasPinSetupAsync()
    {
        var hash = await SecureStorage.Default.GetAsync(PinHashKey);
        return !string.IsNullOrEmpty(hash);
    }

    public async Task SetupPinAsync(string pin, string userName, string password)
    {
        var salt = Guid.NewGuid().ToString("N");
        var hash = Hash(pin, salt);

        await SecureStorage.Default.SetAsync(PinSaltKey, salt);
        await SecureStorage.Default.SetAsync(PinHashKey, hash);
        await SecureStorage.Default.SetAsync(VaultUserNameKey, userName);
        await SecureStorage.Default.SetAsync(VaultPasswordKey, password);
    }

    /// <summary>Re-hashes an already-set-up vault under a new PIN, reusing the existing
    /// vaulted credentials - no need to ask for the password again just to change the PIN.</summary>
    public async Task<bool> ChangePinAsync(string newPin)
    {
        var vaulted = await GetVaultedCredentialsAsync();
        if (vaulted is null)
        {
            return false;
        }

        await SetupPinAsync(newPin, vaulted.Value.UserName, vaulted.Value.Password);
        return true;
    }

    public async Task<bool> VerifyPinAsync(string pin)
    {
        var salt = await SecureStorage.Default.GetAsync(PinSaltKey);
        var storedHash = await SecureStorage.Default.GetAsync(PinHashKey);
        if (string.IsNullOrEmpty(salt) || string.IsNullOrEmpty(storedHash))
        {
            return false;
        }

        return Hash(pin, salt) == storedHash;
    }

    public async Task<(string UserName, string Password)?> GetVaultedCredentialsAsync()
    {
        var userName = await SecureStorage.Default.GetAsync(VaultUserNameKey);
        var password = await SecureStorage.Default.GetAsync(VaultPasswordKey);
        if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
        {
            return null;
        }

        return (userName, password);
    }

    /// <summary>Disables quick access entirely - wipes the PIN and the vaulted credentials.
    /// A plain "Sign out" does NOT call this; it only clears the active session token, so PIN
    /// unlock keeps working next time (matching "no need to relogin every time"). This is for
    /// the explicit "turn off quick access" action.</summary>
    public void Clear()
    {
        SecureStorage.Default.Remove(PinHashKey);
        SecureStorage.Default.Remove(PinSaltKey);
        SecureStorage.Default.Remove(VaultUserNameKey);
        SecureStorage.Default.Remove(VaultPasswordKey);
    }

    private static string Hash(string pin, string salt)
    {
        var bytes = Encoding.UTF8.GetBytes(pin + salt);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}

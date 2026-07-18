using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Garmetix.Api.Configuration;

/// <summary>
/// Encrypts/decrypts secret configuration values (DB passwords, JWT signing key, license master
/// secret, Cloudflare tokens, SMTP passwords, ...) using ASP.NET Core Data Protection - same
/// mechanism and precedent as GstCredentialProtector/EmailCredentialProtector, distinct purpose
/// string so the three never cross-decrypt. Decrypted values are only ever used server-side
/// (rendering an env file that only root can read, or an in-memory write-to-disk operation) -
/// never returned to the frontend in plaintext.
/// </summary>
public sealed class ConfigCredentialProtector
{
    private readonly IDataProtector _protector;

    public ConfigCredentialProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("Garmetix.Configuration.ConfigEntry.v1");
    }

    public string Protect(string plainText) => _protector.Protect(plainText ?? string.Empty);

    public string? Unprotect(string? cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(cipherText);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    /// <summary>Builds a display mask like "••••••••1234" - never shows the stored secret in full again.</summary>
    public static string Mask(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        var visibleCount = plainText.Length > 4 ? Math.Min(4, plainText.Length - 4) : 0;
        var hiddenCount = Math.Max(4, plainText.Length - visibleCount);
        var visibleSuffix = visibleCount > 0 ? plainText[^visibleCount..] : string.Empty;
        return new string('•', Math.Min(hiddenCount, 12)) + visibleSuffix;
    }
}

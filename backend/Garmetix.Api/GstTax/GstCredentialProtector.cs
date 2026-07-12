using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Garmetix.Api.GstTax;

/// <summary>
/// Encrypts/decrypts GST provider secrets (client secret, API key, password, auth token, ...) using
/// ASP.NET Core Data Protection. Ciphertext is what gets stored in gst_api_provider_credentials -
/// the decrypted value is only ever used server-side when actually calling a provider, never returned to the frontend.
/// </summary>
public sealed class GstCredentialProtector
{
    private readonly IDataProtector _protector;

    public GstCredentialProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("Garmetix.GstTax.ProviderCredentials.v1");
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

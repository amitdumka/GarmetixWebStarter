using Microsoft.AspNetCore.DataProtection;

namespace Garmetix.Api.Communication;

/// <summary>
/// Encrypts/decrypts Email provider secrets (SMTP password, Brevo API key, etc). Sibling of
/// GstCredentialProtector, sharing the same process-wide AddDataProtection().PersistKeysToFileSystem(...)
/// key ring (see Program.cs) but isolated from GST ciphertext by its own purpose string -
/// Data Protection purpose strings are what separate modules, not a second key ring.
/// </summary>
public sealed class EmailCredentialProtector
{
    private readonly IDataProtector _protector;

    public EmailCredentialProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("Garmetix.Communication.ProviderCredentials.v1");
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
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }

    public static string Mask(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        if (plainText.Length <= 4)
        {
            return new string('•', 8);
        }

        var visibleSuffix = plainText[^4..];
        return new string('•', 8) + visibleSuffix;
    }
}

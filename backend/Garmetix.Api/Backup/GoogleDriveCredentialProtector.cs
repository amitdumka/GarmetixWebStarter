using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Garmetix.Api.Backup;

/// <summary>
/// Encrypts/decrypts a Company's own Google OAuth refresh token using the same ASP.NET Core Data
/// Protection key ring the GST module's GstCredentialProtector already uses, scoped to its own purpose
/// string so the two are never cross-decryptable. The decrypted token is only ever used server-side to
/// mint a fresh access token before calling the Drive API - never returned to the frontend.
/// </summary>
public sealed class GoogleDriveCredentialProtector
{
    private readonly IDataProtector _protector;

    public GoogleDriveCredentialProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("Garmetix.Backup.GoogleDriveRefreshToken.v1");
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
}

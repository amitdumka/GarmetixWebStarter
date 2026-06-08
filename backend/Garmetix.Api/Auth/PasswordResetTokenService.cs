using System.Security.Cryptography;
using System.Text;

namespace Garmetix.Api.Auth;

public sealed class PasswordResetTokenService(IConfiguration configuration)
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);

    public DateTime ExpiresAtUtc => DateTime.UtcNow.Add(TokenLifetime);

    public string CreateToken(Guid userId)
    {
        var issuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = $"{userId:N}|{issuedAt}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var signature = Sign(payloadBytes);

        return $"{Base64UrlEncode(payloadBytes)}.{Base64UrlEncode(signature)}";
    }

    public bool TryValidate(string token, out Guid userId, out string message)
    {
        userId = Guid.Empty;
        message = "Invalid reset token.";

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var parts = token.Split('.', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        byte[] payloadBytes;
        byte[] suppliedSignature;
        try
        {
            payloadBytes = Base64UrlDecode(parts[0]);
            suppliedSignature = Base64UrlDecode(parts[1]);
        }
        catch
        {
            return false;
        }

        var expectedSignature = Sign(payloadBytes);
        if (suppliedSignature.Length != expectedSignature.Length
            || !CryptographicOperations.FixedTimeEquals(suppliedSignature, expectedSignature))
        {
            return false;
        }

        var payload = Encoding.UTF8.GetString(payloadBytes).Split('|', 2);
        if (payload.Length != 2 || !Guid.TryParseExact(payload[0], "N", out userId))
        {
            return false;
        }

        if (!long.TryParse(payload[1], out var issuedAtSeconds))
        {
            return false;
        }

        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(issuedAtSeconds);
        if (DateTimeOffset.UtcNow - issuedAt > TokenLifetime)
        {
            message = "Reset token has expired. Request a new reset link.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private byte[] Sign(byte[] payloadBytes)
    {
        var signingKey = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is missing.");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
        return hmac.ComputeHash(payloadBytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += padded.Length % 4 switch
        {
            2 => "==",
            3 => "=",
            0 => string.Empty,
            _ => throw new FormatException("Invalid base64url value.")
        };

        return Convert.FromBase64String(padded);
    }
}

using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Garmetix.Api.Communication;

public sealed record WebhookAuthResult(bool IsAuthenticated, string? Reason);

/// <summary>
/// Validates an inbound Brevo webhook call against Basic Auth credentials and/or a header
/// token embedded in the notify URL/webhook config on Brevo's side (see BrevoWebhookOptions
/// for the sourcing of this security model). Fails closed - Enabled=false or no credentials
/// configured means every call is rejected, never silently accepted.
/// </summary>
public sealed class BrevoWebhookAuthenticator(IOptionsMonitor<BrevoWebhookOptions> optionsMonitor)
{
    public WebhookAuthResult Authenticate(HttpContext context)
    {
        var options = optionsMonitor.CurrentValue;
        if (!options.Enabled)
        {
            return new WebhookAuthResult(false, "Brevo webhook is disabled (Communication:BrevoWebhook:Enabled=false).");
        }

        var hasBasicAuthConfigured = !string.IsNullOrWhiteSpace(options.BasicAuthUsername) && !string.IsNullOrWhiteSpace(options.BasicAuthPassword);
        var hasHeaderTokenConfigured = !string.IsNullOrWhiteSpace(options.HeaderToken);
        if (!hasBasicAuthConfigured && !hasHeaderTokenConfigured)
        {
            return new WebhookAuthResult(false, "No webhook credentials configured - refusing to accept unauthenticated calls.");
        }

        var credentialAuthenticated = false;

        if (hasBasicAuthConfigured && TryGetBasicAuthCredentials(context, out var username, out var password))
        {
            credentialAuthenticated = FixedTimeEquals(username, options.BasicAuthUsername!) && FixedTimeEquals(password, options.BasicAuthPassword!);
        }

        if (!credentialAuthenticated && hasHeaderTokenConfigured)
        {
            var headerName = string.IsNullOrWhiteSpace(options.HeaderName) ? "X-Webhook-Token" : options.HeaderName;
            var suppliedToken = context.Request.Headers[headerName].ToString();
            credentialAuthenticated = !string.IsNullOrEmpty(suppliedToken) && FixedTimeEquals(suppliedToken, options.HeaderToken!);
        }

        if (!credentialAuthenticated)
        {
            return new WebhookAuthResult(false, "Invalid or missing webhook credentials.");
        }

        if (options.AllowedIpCidrRanges.Count > 0)
        {
            var remoteIp = context.Connection.RemoteIpAddress;
            var ipAllowed = remoteIp is not null && options.AllowedIpCidrRanges.Any(cidr => IsInRange(remoteIp, cidr));
            if (!ipAllowed)
            {
                return new WebhookAuthResult(false, $"Caller IP {remoteIp} is outside the configured allow-list.");
            }
        }

        return new WebhookAuthResult(true, null);
    }

    private static bool TryGetBasicAuthCredentials(HttpContext context, out string username, out string password)
    {
        username = string.Empty;
        password = string.Empty;

        var header = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(header) || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..].Trim()));
            var separatorIndex = decoded.IndexOf(':');
            if (separatorIndex < 0)
            {
                return false;
            }

            username = decoded[..separatorIndex];
            password = decoded[(separatorIndex + 1)..];
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var bytesA = Encoding.UTF8.GetBytes(a);
        var bytesB = Encoding.UTF8.GetBytes(b);
        if (bytesA.Length != bytesB.Length)
        {
            // Still run a comparison of equal-length buffers to avoid a trivial length-based timing leak.
            return CryptographicOperations.FixedTimeEquals(bytesA, bytesA) && false;
        }

        return CryptographicOperations.FixedTimeEquals(bytesA, bytesB);
    }

    private static bool IsInRange(IPAddress address, string cidr)
    {
        try
        {
            var network = IPNetwork.Parse(cidr);
            return network.Contains(address);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

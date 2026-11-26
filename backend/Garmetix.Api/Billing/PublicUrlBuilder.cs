using System.Net;

namespace Garmetix.Api.Billing;

/// <summary>
/// Builds customer-facing public URLs (receipt policy links, etc.). A request made over the LAN
/// (raw IP address, localhost, *.local, or an explicit port) must never leak into a printed
/// invoice, so those hosts fall back to the public domain.
/// </summary>
public static class PublicUrlBuilder
{
    private const string FallbackHost = "srp.aadwikafashion.in";

    public static string GoodsReturnPolicy(HttpContext context, IConfiguration configuration)
    {
        var configuredBase = configuration["DigitalBills:PublicBaseUrl"]?.Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(configuredBase))
        {
            return $"{configuredBase}/goods-return-policy";
        }

        var host = context.Request.Host;
        if (!host.HasValue || IsInternalHost(host))
        {
            return $"https://{FallbackHost}/goods-return-policy";
        }

        var scheme = string.IsNullOrWhiteSpace(context.Request.Scheme) ? "https" : context.Request.Scheme;
        return $"{scheme}://{host.Value}/goods-return-policy";
    }

    public static bool IsInternalHost(HostString host)
    {
        var name = host.Host;
        if (string.IsNullOrWhiteSpace(name))
        {
            return true;
        }

        if (IPAddress.TryParse(name.Trim('[', ']'), out _))
        {
            return true;
        }

        return name.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".lan", StringComparison.OrdinalIgnoreCase)
            || !name.Contains('.');
    }
}

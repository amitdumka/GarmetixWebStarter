using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Garmetix.Core.Models.Inventory;
using Microsoft.Extensions.Options;

namespace Garmetix.Api.Gstin;

public sealed class GstinLookupService(HttpClient httpClient, IOptions<GstinLookupOptions> options, ILogger<GstinLookupService> logger)
{
    private static readonly Regex GstinPattern = new(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly GstinLookupOptions _options = options.Value;


public GstinProviderStatusDto GetProviderStatus()
{
    var issues = new List<string>();
    var recommendations = new List<string>();
    var enabled = _options.Enabled;
    var hasBaseUrl = !string.IsNullOrWhiteSpace(_options.BaseUrl) || Uri.TryCreate(_options.UrlTemplate, UriKind.Absolute, out _);
    var hasSourceName = !string.IsNullOrWhiteSpace(_options.SourceName);
    var hasApiKey = !string.IsNullOrWhiteSpace(_options.ApiKey);
    var headerName = string.IsNullOrWhiteSpace(_options.ApiKeyHeaderName) ? "x-api-key" : _options.ApiKeyHeaderName.Trim();

    if (!enabled)
    {
        issues.Add("GSTIN lookup provider is disabled. Only local format/state-code validation will run.");
        recommendations.Add("Set GSTIN_LOOKUP_ENABLED=true after buying/configuring a GSTIN provider plan.");
    }

    if (enabled && !hasBaseUrl)
    {
        issues.Add("GSTIN provider base URL is missing.");
        recommendations.Add("Set GSTIN_LOOKUP_BASE_URL or GSTIN_LOOKUP_URL_TEMPLATE in .env.production.");
    }

    if (enabled && !hasApiKey)
    {
        issues.Add("GSTIN provider API key is missing.");
        recommendations.Add("Set GSTIN_LOOKUP_API_KEY and keep it outside source control.");
    }

    if (!hasSourceName || _options.SourceName.Contains("Configured", StringComparison.OrdinalIgnoreCase))
    {
        recommendations.Add("Set GSTIN_LOOKUP_SOURCE_NAME to the licensed provider name for audit visibility.");
    }

    if (string.IsNullOrWhiteSpace(headerName))
    {
        issues.Add("GSTIN API key header name is empty.");
    }

    var ready = enabled && hasBaseUrl && hasApiKey && issues.Count == 0;
    return new GstinProviderStatusDto(
        enabled,
        ready,
        SourceName(),
        MaskUrl(_options.BaseUrl),
        MaskUrl(_options.UrlTemplate),
        headerName,
        Math.Clamp(_options.TimeoutSeconds, 5, 60),
        issues,
        recommendations);
}

    public async Task<GstinLookupResponse> LookupAsync(string? gstin, CancellationToken cancellationToken)
    {
        var normalized = NormalizeGstin(gstin);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new GstinLookupResponse(string.Empty, false, false, "Invalid", null, null, null, null, null, "Local Validation", null, "GSTIN is required.");
        }

        if (!GstinPattern.IsMatch(normalized))
        {
            return new GstinLookupResponse(normalized, false, false, "Invalid", null, null, null, StateCode(normalized), null, "Local Validation", null, "GSTIN format is invalid.");
        }

        if (!_options.Enabled || (string.IsNullOrWhiteSpace(_options.BaseUrl) && !Uri.TryCreate(_options.UrlTemplate, UriKind.Absolute, out _)))
        {
            return new GstinLookupResponse(
                normalized,
                true,
                false,
                "ProviderDisabled",
                null,
                null,
                null,
                StateCode(normalized),
                null,
                "Local Validation",
                null,
                "GSTIN provider is disabled. Format and state code were checked locally, but legal name/address were not fetched.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BuildLookupUrl(normalized));
            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                if (string.Equals(_options.ApiKeyHeaderName, "Authorization", StringComparison.OrdinalIgnoreCase))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
                }
                else
                {
                    request.Headers.TryAddWithoutValidation(_options.ApiKeyHeaderName, _options.ApiKey);
                }
            }

            httpClient.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 60));
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new GstinLookupResponse(normalized, true, false, "LookupFailed", null, null, null, StateCode(normalized), null, SourceName(), null, $"GSTIN lookup failed with HTTP {(int)response.StatusCode}.");
            }

            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            var legalName = FindString(root, "legalName", "LegalName", "legal_name", "lgnm", "data.lgnm", "result.lgnm", "data.legalName", "result.legalName");
            var tradeName = FindString(root, "tradeName", "TradeName", "trade_name", "tradeNam", "data.tradeNam", "result.tradeNam", "data.tradeName", "result.tradeName");
            var status = FindString(root, "status", "Status", "sts", "data.sts", "result.sts") ?? "Active";
            var taxpayerType = FindString(root, "taxpayerType", "TaxpayerType", "ctb", "data.ctb", "result.ctb");
            var address = FindString(root, "principalAddress", "PrincipalAddress", "address", "data.principalAddress", "result.principalAddress", "pradr.adr", "data.pradr.adr", "result.pradr.adr")
                ?? BuildProviderAddress(root);

            return new GstinLookupResponse(
                normalized,
                true,
                !string.IsNullOrWhiteSpace(legalName) || !string.IsNullOrWhiteSpace(tradeName),
                status,
                legalName,
                tradeName,
                address,
                StateCode(normalized),
                taxpayerType,
                SourceName(),
                DateTime.UtcNow,
                null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "GSTIN lookup failed for {Gstin}.", normalized);
            return new GstinLookupResponse(normalized, true, false, "LookupFailed", null, null, null, StateCode(normalized), null, SourceName(), null, ex.Message);
        }
    }

    public async Task<PartyGstinValidationResponse> ValidatePartyAsync(string partyType, string? gstin, string? name, string? address, CancellationToken cancellationToken)
    {
        var lookup = await LookupAsync(gstin, cancellationToken);
        var alerts = new List<string>();

        if (!lookup.IsValidFormat)
        {
            alerts.Add(lookup.Message ?? "GSTIN format is invalid.");
        }
        else if (!lookup.IsVerified)
        {
            alerts.Add(lookup.Message ?? "GSTIN legal name/address could not be fetched.");
        }

        if (lookup.IsVerified)
        {
            var expectedNames = new[] { lookup.LegalName, lookup.TradeName }.Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>().ToArray();
            if (!string.IsNullOrWhiteSpace(name) && expectedNames.Length > 0 && !expectedNames.Any(expected => LooksSimilar(name, expected)))
            {
                alerts.Add($"Entered {partyType} name does not match GSTIN legal/trade name. GSTIN shows: {string.Join(" / ", expectedNames)}.");
            }

            if (!string.IsNullOrWhiteSpace(address) && !string.IsNullOrWhiteSpace(lookup.PrincipalAddress) && !LooksAddressRelated(address, lookup.PrincipalAddress))
            {
                alerts.Add("Entered address does not look similar to the GSTIN principal address.");
            }

            if (!string.IsNullOrWhiteSpace(lookup.Status) && !lookup.Status.Contains("active", StringComparison.OrdinalIgnoreCase))
            {
                alerts.Add($"GSTIN status is {lookup.Status}. Verify before transacting.");
            }
        }

        return new PartyGstinValidationResponse(lookup, alerts.Count > 0, alerts);
    }

    public void ApplyVerification(Customer customer, PartyGstinValidationResponse validation)
    {
        ApplyVerification(
            validation,
            gstin => customer.GSTIN = gstin,
            value => customer.GSTLegalName = value,
            value => customer.GSTTradeName = value,
            value => customer.GSTPrincipalAddress = value,
            value => customer.GSTStateCode = value,
            value => customer.GSTTaxpayerType = value,
            value => customer.GSTRegistrationStatus = value,
            value => customer.GSTVerified = value,
            value => customer.GSTVerifiedAt = value,
            value => customer.GSTLookupSource = value,
            value => customer.GSTMismatchAlert = value);
    }

    public void ApplyVerification(Vendor vendor, PartyGstinValidationResponse validation)
    {
        ApplyVerification(
            validation,
            gstin => vendor.GSTIN = gstin,
            value => vendor.GSTLegalName = value,
            value => vendor.GSTTradeName = value,
            value => vendor.GSTPrincipalAddress = value,
            value => vendor.GSTStateCode = value,
            value => vendor.GSTTaxpayerType = value,
            value => vendor.GSTRegistrationStatus = value,
            value => vendor.GSTVerified = value,
            value => vendor.GSTVerifiedAt = value,
            value => vendor.GSTLookupSource = value,
            value => vendor.GSTMismatchAlert = value);
    }

    private void ApplyVerification(
        PartyGstinValidationResponse validation,
        Action<string?> setGstin,
        Action<string?> setLegalName,
        Action<string?> setTradeName,
        Action<string?> setPrincipalAddress,
        Action<string?> setStateCode,
        Action<string?> setTaxpayerType,
        Action<string?> setRegistrationStatus,
        Action<bool> setVerified,
        Action<DateTime?> setVerifiedAt,
        Action<string?> setLookupSource,
        Action<string?> setMismatchAlert)
    {
        var lookup = validation.Lookup;
        if (string.IsNullOrWhiteSpace(lookup.Gstin))
        {
            return;
        }

        setGstin(lookup.Gstin);
        setLegalName(lookup.LegalName);
        setTradeName(lookup.TradeName);
        setPrincipalAddress(lookup.PrincipalAddress);
        setStateCode(lookup.StateCode);
        setTaxpayerType(lookup.TaxpayerType);
        setRegistrationStatus(lookup.Status);
        setVerified(lookup.IsVerified);
        setVerifiedAt(lookup.VerifiedAtUtc.HasValue ? DateTime.SpecifyKind(lookup.VerifiedAtUtc.Value, DateTimeKind.Unspecified) : DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified));
        setLookupSource(lookup.Source);
        setMismatchAlert(validation.Alerts.Count == 0 ? null : string.Join(" | ", validation.Alerts));
    }

    private Uri BuildLookupUrl(string gstin)
    {
        var encoded = Uri.EscapeDataString(gstin);
        var template = string.IsNullOrWhiteSpace(_options.UrlTemplate) ? string.Empty : _options.UrlTemplate.Trim();
        string url;
        if (!string.IsNullOrWhiteSpace(template))
        {
            url = template.Replace("{gstin}", encoded, StringComparison.OrdinalIgnoreCase);
            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                url = CombineUrl(_options.BaseUrl, url);
            }
        }
        else
        {
            url = CombineUrl(_options.BaseUrl, encoded);
        }

        return new Uri(url);
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        return $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
    }


private static string MaskUrl(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return string.Empty;
    }

    var text = value.Trim();
    if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
    {
        return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";
    }

    return text.Replace("apikey=", "apikey=***", StringComparison.OrdinalIgnoreCase)
        .Replace("api_key=", "api_key=***", StringComparison.OrdinalIgnoreCase)
        .Replace("token=", "token=***", StringComparison.OrdinalIgnoreCase);
}

    private string SourceName() => string.IsNullOrWhiteSpace(_options.SourceName) ? "Configured GSTIN Provider" : _options.SourceName.Trim();

    public static string NormalizeGstin(string? gstin) => string.IsNullOrWhiteSpace(gstin) ? string.Empty : gstin.Trim().ToUpperInvariant();

    private static string? StateCode(string gstin) => gstin.Length >= 2 ? gstin[..2] : null;

    private static string? FindString(JsonElement root, params string[] paths)
    {
        foreach (var path in paths)
        {
            if (TryReadPath(root, path.Split('.', StringSplitOptions.RemoveEmptyEntries), out var value) && value.ValueKind != JsonValueKind.Null)
            {
                var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text.Trim();
                }
            }
        }

        return null;
    }

    private static bool TryReadPath(JsonElement root, IReadOnlyList<string> segments, out JsonElement value)
    {
        value = root;
        foreach (var segment in segments)
        {
            if (value.ValueKind != JsonValueKind.Object || !TryGetPropertyIgnoreCase(value, segment, out value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? BuildProviderAddress(JsonElement root)
    {
        var addressParts = new[]
        {
            FindString(root, "pradr.addr.bnm", "data.pradr.addr.bnm", "result.pradr.addr.bnm"),
            FindString(root, "pradr.addr.st", "data.pradr.addr.st", "result.pradr.addr.st"),
            FindString(root, "pradr.addr.loc", "data.pradr.addr.loc", "result.pradr.addr.loc"),
            FindString(root, "pradr.addr.dst", "data.pradr.addr.dst", "result.pradr.addr.dst"),
            FindString(root, "pradr.addr.stcd", "data.pradr.addr.stcd", "result.pradr.addr.stcd"),
            FindString(root, "pradr.addr.pncd", "data.pradr.addr.pncd", "result.pradr.addr.pncd")
        }.Where(part => !string.IsNullOrWhiteSpace(part)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        return addressParts.Length == 0 ? null : string.Join(", ", addressParts);
    }

    private static bool LooksSimilar(string left, string right)
    {
        var a = NormalizeCompare(left);
        var b = NormalizeCompare(right);
        return a.Length > 0 && b.Length > 0 && (a.Contains(b, StringComparison.OrdinalIgnoreCase) || b.Contains(a, StringComparison.OrdinalIgnoreCase) || Similarity(a, b) >= 0.72m);
    }

    private static bool LooksAddressRelated(string left, string right)
    {
        var leftWords = NormalizeCompare(left).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(word => word.Length > 3).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rightWords = NormalizeCompare(right).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(word => word.Length > 3).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return leftWords.Count == 0 || rightWords.Count == 0 || leftWords.Intersect(rightWords).Take(2).Count() >= Math.Min(2, Math.Min(leftWords.Count, rightWords.Count));
    }

    private static string NormalizeCompare(string value)
    {
        var normalized = Regex.Replace(value.ToUpperInvariant(), @"[^A-Z0-9]+", " ").Trim();
        var suffixes = new[] { " PRIVATE LIMITED", " PVT LTD", " LIMITED", " LTD", " LLP", " OPC", " INDIA", " AND ", " & " };
        foreach (var suffix in suffixes)
        {
            normalized = normalized.Replace(suffix, " ", StringComparison.OrdinalIgnoreCase);
        }

        return Regex.Replace(normalized, @"\s+", " ").Trim();
    }

    private static decimal Similarity(string left, string right)
    {
        var distance = Levenshtein(left, right);
        var max = Math.Max(left.Length, right.Length);
        return max == 0 ? 1 : 1 - (decimal)distance / max;
    }

    private static int Levenshtein(string left, string right)
    {
        var matrix = new int[left.Length + 1, right.Length + 1];
        for (var i = 0; i <= left.Length; i++) matrix[i, 0] = i;
        for (var j = 0; j <= right.Length; j++) matrix[0, j] = j;

        for (var i = 1; i <= left.Length; i++)
        {
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1), matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[left.Length, right.Length];
    }
}

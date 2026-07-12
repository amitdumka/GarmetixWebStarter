using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Garmetix.Core.Models.GstTax;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.GstTax;

/// <summary>
/// Resolves a GSTIN lookup through the multi-provider registry built in Stage GST-1/2: local cache first
/// (unless force-refresh), then enabled providers with the GSTIN_LOOKUP feature in priority order, falling
/// back to the next provider on failure only if FallbackEnabled. Every attempt is logged. Local Master
/// (cache + format/state-code validation) is always the final honest answer - live billing never blocks on this.
/// </summary>
public sealed class GstinResolutionService(GarmetixDbContext db, GstCredentialProtector protector, IHttpClientFactory httpClientFactory)
{
    private static readonly Regex GstinFormatPattern = new(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string NormalizeGstin(string? gstin) => string.IsNullOrWhiteSpace(gstin) ? string.Empty : gstin.Trim().ToUpperInvariant();

    public async Task<GstinLookupResultDto> VerifyAsync(string? rawGstin, bool forceRefresh, CancellationToken cancellationToken)
    {
        var gstin = NormalizeGstin(rawGstin);
        if (string.IsNullOrEmpty(gstin))
        {
            return new GstinLookupResultDto(false, gstin, null, null, null, null, null, null, null, null, "Local Validation", "GSTIN is required.");
        }

        if (!GstinFormatPattern.IsMatch(gstin))
        {
            return new GstinLookupResultDto(false, gstin, null, null, null, null, await StateNameForAsync(gstin, cancellationToken), null, null, null, "Local Validation", $"'{gstin}' does not match the GSTIN format.");
        }

        if (!forceRefresh)
        {
            var cached = await db.GstinVerificationCaches.AsNoTracking().FirstOrDefaultAsync(c => c.Gstin == gstin, cancellationToken);
            if (cached is not null)
            {
                return FromCache(cached);
            }
        }

        var providers = await db.GstApiProviders.AsNoTracking()
            .Where(p => p.IsEnabled && p.ProviderType != "LocalMasterOnly")
            .OrderBy(p => p.Priority)
            .ToListAsync(cancellationToken);
        var providerIds = providers.Select(p => p.Id).ToArray();
        var eligibleIds = await db.GstApiProviderFeatures.AsNoTracking()
            .Where(f => providerIds.Contains(f.ProviderId) && f.FeatureCode == "GSTIN_LOOKUP" && f.IsEnabled)
            .Select(f => f.ProviderId)
            .ToListAsync(cancellationToken);
        var eligibleProviders = providers.Where(p => eligibleIds.Contains(p.Id)).ToList();

        foreach (var provider in eligibleProviders)
        {
            var stopwatch = Stopwatch.StartNew();
            GstinLookupResultDto? result = null;
            string? error;

            try
            {
                if (provider.ProviderType == "GenericRestProvider")
                {
                    result = await CallGenericRestProviderAsync(provider, gstin, cancellationToken);
                    error = result.Success ? null : result.ErrorMessage;
                }
                else
                {
                    error = $"Live GSTIN lookup for provider type '{provider.ProviderType}' arrives in a later GST & Taxes stage.";
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                error = ex.Message;
            }

            stopwatch.Stop();
            await LogAsync(provider.Id, provider.ProviderName, gstin, result?.Success ?? false, error, (int)stopwatch.ElapsedMilliseconds, cancellationToken);

            if (result is { Success: true })
            {
                await UpsertCacheAsync(gstin, result, provider.ProviderName, cancellationToken);
                return result;
            }

            if (!provider.FallbackEnabled)
            {
                break;
            }
        }

        return new GstinLookupResultDto(false, gstin, null, null, null, null, await StateNameForAsync(gstin, cancellationToken), null, null, null, "Local Validation", "Format and state code checked locally. No configured provider returned verified details - configure a GSTIN provider in GST & Taxes -> API Setup, or enter details manually.");
    }

    private async Task<GstinLookupResultDto> CallGenericRestProviderAsync(GstApiProvider provider, string gstin, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(provider.BaseUrl))
        {
            return new GstinLookupResultDto(false, gstin, null, null, null, null, null, null, null, null, provider.ProviderName, "Provider has no Base URL configured.");
        }

        var credentials = await db.GstApiProviderCredentials.AsNoTracking()
            .Where(c => c.ProviderId == provider.Id)
            .ToDictionaryAsync(c => c.CredentialKey, c => protector.Unprotect(c.EncryptedValue), cancellationToken);

        var client = httpClientFactory.CreateClient("GstGenericRestProvider");
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(provider.TimeoutSeconds, 5, 120));

        using var request = new HttpRequestMessage(HttpMethod.Get, CombineUrl(provider.BaseUrl, $"gstin/{Uri.EscapeDataString(gstin)}"));
        if (credentials.TryGetValue("API_KEY", out var apiKey) && !string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
        }
        else if (credentials.TryGetValue("AUTH_TOKEN", out var authToken) && !string.IsNullOrWhiteSpace(authToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
        }

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new GstinLookupResultDto(false, gstin, null, null, null, null, null, null, null, null, provider.ProviderName, $"Provider returned HTTP {(int)response.StatusCode}.");
        }

        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        var legalName = FindString(root, "legalName", "legal_name", "lgnm", "data.lgnm", "data.legalName");
        var tradeName = FindString(root, "tradeName", "trade_name", "tradeNam", "data.tradeNam", "data.tradeName");
        var status = FindString(root, "status", "sts", "data.sts") ?? "Active";
        var taxpayerType = FindString(root, "taxpayerType", "ctb", "data.ctb");
        var address = FindString(root, "principalAddress", "address", "data.principalAddress", "pradr.adr", "data.pradr.adr");
        var stateCode = gstin.Length >= 2 ? gstin[..2] : null;

        var success = !string.IsNullOrWhiteSpace(legalName) || !string.IsNullOrWhiteSpace(tradeName);
        return new GstinLookupResultDto(
            success,
            gstin,
            legalName,
            tradeName,
            taxpayerType,
            status,
            stateCode,
            await StateNameForAsync(gstin, cancellationToken),
            address,
            DateTime.UtcNow,
            provider.ProviderName,
            success ? null : "Provider response did not include a legal or trade name.");
    }

    private async Task UpsertCacheAsync(string gstin, GstinLookupResultDto result, string source, CancellationToken cancellationToken)
    {
        var entry = await db.GstinVerificationCaches.FirstOrDefaultAsync(c => c.Gstin == gstin, cancellationToken);
        if (entry is null)
        {
            entry = new GstinVerificationCache { Gstin = gstin };
            db.GstinVerificationCaches.Add(entry);
        }

        entry.LegalName = result.LegalName;
        entry.TradeName = result.TradeName;
        entry.TaxpayerType = result.TaxpayerType;
        entry.RegistrationStatus = result.RegistrationStatus;
        entry.StateCode = result.StateCode;
        entry.StateName = result.StateName;
        entry.PrincipalAddress = result.PrincipalAddress;
        entry.LastVerifiedAt = DateTime.UtcNow;
        entry.VerificationSource = source;
        entry.IsActive = result.RegistrationStatus is null || result.RegistrationStatus.Contains("active", StringComparison.OrdinalIgnoreCase);

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<GstinLookupResultDto> SaveManualAsync(GstinManualCacheRequest request, CancellationToken cancellationToken)
    {
        var gstin = NormalizeGstin(request.Gstin);
        var entry = await db.GstinVerificationCaches.FirstOrDefaultAsync(c => c.Gstin == gstin, cancellationToken);
        if (entry is null)
        {
            entry = new GstinVerificationCache { Gstin = gstin };
            db.GstinVerificationCaches.Add(entry);
        }

        entry.LegalName = request.LegalName;
        entry.TradeName = request.TradeName;
        entry.TaxpayerType = request.TaxpayerType;
        entry.RegistrationStatus = request.RegistrationStatus;
        entry.StateCode = request.StateCode ?? (gstin.Length >= 2 ? gstin[..2] : null);
        entry.StateName = request.StateName ?? await StateNameForAsync(gstin, cancellationToken);
        entry.PrincipalAddress = request.PrincipalAddress;
        entry.LastVerifiedAt = DateTime.UtcNow;
        entry.VerificationSource = "Manual Entry";
        entry.IsActive = request.RegistrationStatus is null || request.RegistrationStatus.Contains("active", StringComparison.OrdinalIgnoreCase);

        await db.SaveChangesAsync(cancellationToken);
        return FromCache(entry);
    }

    private async Task LogAsync(Guid providerId, string providerName, string gstin, bool success, string? error, int durationMs, CancellationToken cancellationToken)
    {
        db.GstApiCallLogs.Add(new GstApiCallLog
        {
            ProviderId = providerId,
            FeatureCode = "GSTIN_LOOKUP",
            RequestMethod = "GET",
            IsSuccess = success,
            ErrorMessage = error,
            DurationMs = durationMs,
            Gstin = gstin
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<string?> StateNameForAsync(string gstin, CancellationToken cancellationToken)
    {
        if (gstin.Length < 2)
        {
            return null;
        }

        var code = gstin[..2];
        return await db.GstStateCodes.AsNoTracking().Where(s => s.StateCode == code).Select(s => s.StateName).FirstOrDefaultAsync(cancellationToken);
    }

    private static GstinLookupResultDto FromCache(GstinVerificationCache cached) => new(
        true,
        cached.Gstin,
        cached.LegalName,
        cached.TradeName,
        cached.TaxpayerType,
        cached.RegistrationStatus,
        cached.StateCode,
        cached.StateName,
        cached.PrincipalAddress,
        cached.LastVerifiedAt,
        $"{cached.VerificationSource ?? "Cache"} (cached)",
        null);

    private static string CombineUrl(string baseUrl, string path) => $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";

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
}

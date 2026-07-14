using System.Net.Http.Json;
using Garmetix.AttendanceKiosk.Models;

namespace Garmetix.AttendanceKiosk.Services;

public sealed class KioskApiClient
{
    private readonly HttpClient _http = new();

    public async Task<KioskReadinessResponse?> ReadinessAsync(KioskSettings settings, CancellationToken cancellationToken = default)
    {
        var url = ApiUrl(settings, "attendance/kiosk/readiness");
        var request = new KioskReadinessRequest(Guid.Parse(settings.DeviceId), settings.DeviceToken);
        return await _http.PostAsJsonAsync(url, request, cancellationToken)
            .ResultOrThrow<KioskReadinessResponse>(cancellationToken);
    }

    public async Task<IReadOnlyList<EmployeeLookupResponse>> LookupEmployeeAsync(KioskSettings settings, string search, CancellationToken cancellationToken = default)
    {
        var url = ApiUrl(settings, "attendance/kiosk/lookup-employee");
        var request = new EmployeeLookupRequest(Guid.Parse(settings.DeviceId), settings.DeviceToken, search);
        return await _http.PostAsJsonAsync(url, request, cancellationToken)
            .ResultOrThrow<IReadOnlyList<EmployeeLookupResponse>>(cancellationToken)
            ?? [];
    }

    public async Task<bool> PunchAsync(KioskSettings settings, KioskPunchRequest request, CancellationToken cancellationToken = default)
    {
        var url = ApiUrl(settings, "attendance/kiosk/punch");
        using var response = await _http.PostAsJsonAsync(url, request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<SyncPendingResponse?> SyncPendingAsync(KioskSettings settings, IReadOnlyList<KioskPunchRequest> punches, CancellationToken cancellationToken = default)
    {
        var url = ApiUrl(settings, "attendance/kiosk/sync-pending");
        var request = new SyncPendingRequest(Guid.Parse(settings.DeviceId), settings.DeviceToken, punches);
        return await _http.PostAsJsonAsync(url, request, cancellationToken)
            .ResultOrThrow<SyncPendingResponse>(cancellationToken);
    }

    private static string ApiUrl(KioskSettings settings, string path)
        => $"{settings.ApiBaseUrl.TrimEnd('/')}/api/{path.TrimStart('/')}";
}

file static class HttpResponseMessageExtensions
{
    public static async Task<T?> ResultOrThrow<T>(this Task<HttpResponseMessage> responseTask, CancellationToken cancellationToken)
    {
        using var response = await responseTask;
        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(message) ? response.ReasonPhrase : message);
        }
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }
}

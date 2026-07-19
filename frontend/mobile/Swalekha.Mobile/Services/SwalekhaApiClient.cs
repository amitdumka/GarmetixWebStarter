using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Swalekha.Mobile.Models;

namespace Swalekha.Mobile.Services;

public sealed class SwalekhaApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;

    public SwalekhaApiClient(IHttpClientFactory httpClientFactory)
    {
        _client = httpClientFactory.CreateClient("SwalekhaAuthenticated");
    }

    public Task<SwalekhaDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
        => GetAsync<SwalekhaDashboardDto>("api/swalekha/dashboard", cancellationToken);

    public Task<List<SwalekhaAccountDto>> GetAccountsAsync(CancellationToken cancellationToken = default)
        => GetAsync<List<SwalekhaAccountDto>>("api/swalekha/accounts", cancellationToken);

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _client.GetAsync(path, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new SwalekhaApiException($"Couldn't reach the server at {ApiSettings.BaseUrl}. Check your connection.");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new SwalekhaApiException("Your session has expired. Sign in again.", 401);
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            throw new SwalekhaApiException("This account isn't allowed to use Swalekha.", 403);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new SwalekhaApiException($"Request failed ({(int)response.StatusCode}).", (int)response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new SwalekhaApiException("The server returned an empty response.");
    }
}

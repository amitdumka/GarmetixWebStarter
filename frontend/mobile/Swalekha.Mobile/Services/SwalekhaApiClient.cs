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
        => SendAsync<SwalekhaDashboardDto>(HttpMethod.Get, "api/swalekha/dashboard", null, cancellationToken);

    public Task<List<SwalekhaAccountDto>> GetAccountsAsync(CancellationToken cancellationToken = default)
        => SendAsync<List<SwalekhaAccountDto>>(HttpMethod.Get, "api/swalekha/accounts", null, cancellationToken);

    public Task<SwalekhaAccountDto> GetAccountAsync(Guid id, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaAccountDto>(HttpMethod.Get, $"api/swalekha/accounts/{id}", null, cancellationToken);

    public Task<SwalekhaAccountDto> CreateAccountAsync(SwalekhaAccountPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaAccountDto>(HttpMethod.Post, "api/swalekha/accounts", payload, cancellationToken);

    public Task<SwalekhaAccountDto> UpdateAccountAsync(Guid id, SwalekhaAccountPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaAccountDto>(HttpMethod.Put, $"api/swalekha/accounts/{id}", payload, cancellationToken);

    public Task DeleteAccountAsync(Guid id, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Delete, $"api/swalekha/accounts/{id}", null, cancellationToken);

    public Task<SwalekhaTransactionList> GetAccountTransactionsAsync(Guid accountId, int page, int pageSize, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaTransactionList>(HttpMethod.Get, $"api/swalekha/accounts/{accountId}/transactions?page={page}&pageSize={pageSize}", null, cancellationToken);

    public Task<SwalekhaTransactionDto> AddTransactionAsync(Guid accountId, SwalekhaTransactionPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaTransactionDto>(HttpMethod.Post, $"api/swalekha/accounts/{accountId}/transactions", payload, cancellationToken);

    public Task DeleteTransactionAsync(Guid accountId, Guid transactionId, CancellationToken cancellationToken = default)
        => SendNoContentAsync(HttpMethod.Delete, $"api/swalekha/accounts/{accountId}/transactions/{transactionId}", null, cancellationToken);

    public Task<SwalekhaTransferResult> CreateTransferAsync(SwalekhaTransferPayload payload, CancellationToken cancellationToken = default)
        => SendAsync<SwalekhaTransferResult>(HttpMethod.Post, "api/swalekha/transfers", payload, cancellationToken);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        var response = await SendCoreAsync(method, path, body, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new SwalekhaApiException("The server returned an empty response.");
    }

    private async Task SendNoContentAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
        => await SendCoreAsync(method, path, body, cancellationToken);

    private async Task<HttpResponseMessage> SendCoreAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body, options: JsonOptions);
            }

            response = await _client.SendAsync(request, cancellationToken);
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
            var detail = await TryReadMessageAsync(response, cancellationToken);
            throw new SwalekhaApiException(detail ?? $"Request failed ({(int)response.StatusCode}).", (int)response.StatusCode);
        }

        return response;
    }

    private static async Task<string?> TryReadMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return doc.RootElement.TryGetProperty("message", out var message) ? message.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}

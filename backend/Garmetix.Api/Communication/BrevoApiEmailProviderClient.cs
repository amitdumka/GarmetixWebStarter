using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Garmetix.Core.Models.Communication;

namespace Garmetix.Api.Communication;

/// <summary>
/// Brevo transactional email HTTP API client (https://api.brevo.com/v3/smtp/email).
/// Never logs the "api-key" header - only the sanitized response body (which contains no
/// secrets, just a messageId) is ever passed back as RawResponseForLogging.
/// NOTE: built against the documented Brevo v3 transactional email API shape; not exercised
/// against a live Brevo account in this sandbox (no API key configured here) - flagged per
/// this project's established "no live click-through possible" disclosure convention.
/// </summary>
public sealed class BrevoApiEmailProviderClient(HttpClient httpClient) : ITransactionalEmailProviderClient
{
    private const string DefaultBaseUrl = "https://api.brevo.com";

    public async Task<EmailSendResult> SendAsync(
        EmailProviderConfiguration provider,
        IReadOnlyDictionary<string, string> credentials,
        EmailSendRequest request,
        CancellationToken cancellationToken)
    {
        if (!credentials.TryGetValue("ApiKey", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
        {
            return EmailSendResult.PermanentFailure("MissingApiKey", "Brevo provider has no API key configured.");
        }

        var payload = new BrevoSendPayload
        {
            Sender = new BrevoRecipient(request.From.Email, request.From.DisplayName),
            To = request.To.Select(item => new BrevoRecipient(item.Email, item.DisplayName)).ToList(),
            Cc = request.Cc?.Select(item => new BrevoRecipient(item.Email, item.DisplayName)).ToList(),
            Bcc = request.Bcc?.Select(item => new BrevoRecipient(item.Email, item.DisplayName)).ToList(),
            ReplyTo = request.ReplyToEmail is { Length: > 0 } replyTo ? new BrevoRecipient(replyTo, null) : null,
            Subject = request.Subject,
            HtmlContent = request.HtmlBody,
            TextContent = request.TextBody,
            Attachment = request.Attachments?.Select(a => new BrevoAttachment(a.FileName, Convert.ToBase64String(a.Content))).ToList(),
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildUrl(provider, "/v3/smtp/email"))
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };
        httpRequest.Headers.Add("api-key", apiKey);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var parsed = TryParse<BrevoSendResponse>(body);
                return EmailSendResult.Success(parsed?.MessageId, SanitizeForLogging(body));
            }

            var isTransient = response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                || (int)response.StatusCode >= 500;
            var errorCode = $"Brevo_{(int)response.StatusCode}";
            var errorMessage = $"Brevo API returned {(int)response.StatusCode} {response.ReasonPhrase}";

            return isTransient
                ? EmailSendResult.TransientFailure(errorCode, errorMessage, SanitizeForLogging(body))
                : EmailSendResult.PermanentFailure(errorCode, errorMessage, SanitizeForLogging(body));
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return EmailSendResult.TransientFailure("Brevo_Timeout", "Brevo API request timed out.");
        }
        catch (HttpRequestException ex)
        {
            return EmailSendResult.TransientFailure("Brevo_NetworkError", $"Network error contacting Brevo API: {ex.Message}");
        }
    }

    public async Task<EmailConnectionTestResult> TestConnectionAsync(
        EmailProviderConfiguration provider,
        IReadOnlyDictionary<string, string> credentials,
        CancellationToken cancellationToken)
    {
        if (!credentials.TryGetValue("ApiKey", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
        {
            return new EmailConnectionTestResult(false, "No API key configured for this Brevo provider.");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, BuildUrl(provider, "/v3/account"));
        httpRequest.Headers.Add("api-key", apiKey);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
            return response.IsSuccessStatusCode
                ? new EmailConnectionTestResult(true, "Brevo API key is valid and reachable.")
                : new EmailConnectionTestResult(false, $"Brevo API rejected the key ({(int)response.StatusCode} {response.ReasonPhrase}).");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new EmailConnectionTestResult(false, $"Could not reach the Brevo API: {ex.Message}");
        }
    }

    /// <summary>For ProviderType=BrevoApi, EmailProviderConfiguration.Host doubles as an optional base-URL override (e.g. a proxy); Port/EnableSsl/UseStartTls are unused for this client.</summary>
    private static string BuildUrl(EmailProviderConfiguration provider, string path) =>
        (string.IsNullOrWhiteSpace(provider.Host) ? DefaultBaseUrl : provider.Host) + path;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static T? TryParse<T>(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    /// <summary>Brevo's own responses contain no secrets, but truncate defensively so a malformed huge body never bloats the attempt log.</summary>
    private static string SanitizeForLogging(string body) => body.Length > 2000 ? body[..2000] : body;

    private sealed class BrevoSendPayload
    {
        [JsonPropertyName("sender")] public required BrevoRecipient Sender { get; init; }
        [JsonPropertyName("to")] public required List<BrevoRecipient> To { get; init; }
        [JsonPropertyName("cc")] public List<BrevoRecipient>? Cc { get; init; }
        [JsonPropertyName("bcc")] public List<BrevoRecipient>? Bcc { get; init; }
        [JsonPropertyName("replyTo")] public BrevoRecipient? ReplyTo { get; init; }
        [JsonPropertyName("subject")] public required string Subject { get; init; }
        [JsonPropertyName("htmlContent")] public required string HtmlContent { get; init; }
        [JsonPropertyName("textContent")] public string? TextContent { get; init; }
        [JsonPropertyName("attachment")] public List<BrevoAttachment>? Attachment { get; init; }
    }

    private sealed record BrevoRecipient([property: JsonPropertyName("email")] string Email, [property: JsonPropertyName("name")] string? Name);

    private sealed record BrevoAttachment([property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("content")] string ContentBase64);

    private sealed record BrevoSendResponse([property: JsonPropertyName("messageId")] string? MessageId);
}

using System.Text.Json.Serialization;

namespace Garmetix.Api.Communication;

/// <summary>
/// Shape confirmed against Brevo's transactional webhook documentation
/// (https://developers.brevo.com/docs/how-to-use-webhooks): event, email, message-id, and one
/// of date/ts/ts_epoch for the event time. Fields are all nullable/optional - Brevo's payload
/// shape is not guaranteed identical across every event type, and this must never throw on an
/// unexpected/missing field.
/// </summary>
public sealed class BrevoWebhookEventPayload
{
    [JsonPropertyName("event")] public string? Event { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("message-id")] public string? MessageId { get; set; }
    [JsonPropertyName("date")] public string? Date { get; set; }
    [JsonPropertyName("ts_epoch")] public long? TsEpochMs { get; set; }
    [JsonPropertyName("ts")] public long? TsSeconds { get; set; }
    [JsonPropertyName("reason")] public string? Reason { get; set; }
    [JsonPropertyName("subject")] public string? Subject { get; set; }
}

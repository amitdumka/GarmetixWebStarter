namespace Garmetix.Api.Communication;

/// <summary>
/// Bound from "Communication:BrevoWebhook". Brevo's own documented webhook security model
/// (per https://developers.brevo.com/docs/how-to-use-webhooks and
/// https://help.brevo.com/hc/en-us/articles/15127404548498) is Basic Auth credentials embedded
/// in the notify URL and/or a custom header token configured in the Brevo dashboard, plus an
/// IP allow-list (Brevo's published outbound range is 1.179.112.0/20) - there is no HMAC
/// request-signature scheme for transactional webhooks the way Stripe/GitHub webhooks have one.
/// Fails closed by default: with no BasicAuthUsername/HeaderToken configured, every call is
/// rejected rather than silently accepted.
/// </summary>
public sealed class BrevoWebhookOptions
{
    public bool Enabled { get; set; } = false;
    public string? BasicAuthUsername { get; set; }
    public string? BasicAuthPassword { get; set; }
    public string? HeaderName { get; set; } = "X-Webhook-Token";
    public string? HeaderToken { get; set; }
    /// <summary>Empty = IP check disabled (rely on Basic Auth/header token alone). Populate with Brevo's published CIDR ranges to add defense-in-depth.</summary>
    public List<string> AllowedIpCidrRanges { get; set; } = [];
    public int MaxBodyBytes { get; set; } = 262_144; // 256 KB - Brevo batches events but payloads stay small
}

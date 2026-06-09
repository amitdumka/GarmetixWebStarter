namespace Garmetix.Api.Gstin;

public sealed class GstinLookupOptions
{
    public bool Enabled { get; set; } = false;
    public string BaseUrl { get; set; } = string.Empty;
    public string UrlTemplate { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiKeyHeaderName { get; set; } = "x-api-key";
    public int TimeoutSeconds { get; set; } = 15;
    public string SourceName { get; set; } = "Configured GSTIN Provider";
}

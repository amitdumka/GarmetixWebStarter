using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls(builder.Configuration["Bridge:Urls"] ?? "http://127.0.0.1:8787");
builder.Services.Configure<FingerprintBridgeOptions>(builder.Configuration.GetSection("Bridge"));
builder.Services.AddHttpClient("MantraFingerprintService");
builder.Services.AddSingleton<IFingerprintVendorAdapter>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var options = configuration.GetSection("Bridge").Get<FingerprintBridgeOptions>() ?? new();
    return options.Adapter.Equals("Mantra", StringComparison.OrdinalIgnoreCase)
        ? new MantraFingerprintVendorAdapter(configuration, httpClientFactory)
        : new SimulatorFingerprintVendorAdapter(configuration);
});

var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.Headers["Access-Control-Allow-Origin"] = "*";
    context.Response.Headers["Access-Control-Allow-Headers"] = "content-type";
    context.Response.Headers["Access-Control-Allow-Methods"] = "GET,POST,OPTIONS";
    if (HttpMethods.IsOptions(context.Request.Method))
    {
        context.Response.StatusCode = StatusCodes.Status204NoContent;
        return;
    }

    if (!IsAllowedLocalCaller(context.Connection.RemoteIpAddress))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(BridgeResponse.Failed("Forbidden", "Fingerprint bridge accepts local or private LAN callers only."));
        return;
    }

    context.Response.Headers.CacheControl = "no-store";
    await next();
});

MapBridgeRoutes(app);
MapBridgeRoutes(app.MapGroup("/garmetix-fingerprint"));

app.Run();

static void MapBridgeRoutes(IEndpointRouteBuilder routes)
{
    routes.MapGet("/health", async (IFingerprintVendorAdapter adapter, CancellationToken cancellationToken)
        => Results.Ok(await adapter.HealthAsync(cancellationToken)));

    routes.MapPost("/capture", async (BridgeRequest request, IFingerprintVendorAdapter adapter, CancellationToken cancellationToken)
        => Results.Ok(await adapter.CaptureAsync(request, cancellationToken)));

    routes.MapPost("/identify", async (BridgeRequest request, IFingerprintVendorAdapter adapter, CancellationToken cancellationToken)
        => Results.Ok(await adapter.IdentifyAsync(request, cancellationToken)));

    routes.MapPost("/enroll", async (BridgeRequest request, IFingerprintVendorAdapter adapter, CancellationToken cancellationToken)
        => Results.Ok(await adapter.EnrollAsync(request, cancellationToken)));
}

static bool IsAllowedLocalCaller(IPAddress? address)
{
    if (address is null || IPAddress.IsLoopback(address))
    {
        return true;
    }

    if (address.IsIPv4MappedToIPv6)
    {
        address = address.MapToIPv4();
    }

    var bytes = address.GetAddressBytes();
    return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
        && (bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            || (bytes[0] == 192 && bytes[1] == 168));
}

public sealed record FingerprintBridgeOptions
{
    public string Adapter { get; init; } = "Simulator";
    public string SelectedHardware { get; init; } = "Mantra MFS100 / MIS100";
    public string Vendor { get; init; } = "Garmetix Simulator Fingerprint Bridge";
    public string DeviceSerial { get; init; } = "SIM-FP-BRIDGE-0001";
    public int QualityScore { get; init; } = 86;
    public string MantraSdkPath { get; init; } = "";
    public string MantraServiceUrl { get; init; } = "";
    public string MantraHealthPath { get; init; } = "/health";
    public string MantraCapturePath { get; init; } = "/capture";
    public string MantraIdentifyPath { get; init; } = "/identify";
    public string MantraEnrollPath { get; init; } = "/enroll";
    public int MantraTimeoutSeconds { get; init; } = 15;
}

public sealed record BridgeRequest(
    Guid? EmployeeId,
    string? EmployeeCode,
    string? EmployeeName,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    DateTimeOffset? RequestedAtUtc,
    bool RawPayloadAllowed);

public sealed record BridgeResponse(
    bool Success,
    string Message,
    string BridgeMode,
    string Vendor,
    string DeviceSerial,
    string MatchStatus,
    Guid? EmployeeId,
    string EmployeeCode,
    string EmployeeName,
    string? TemplateRef,
    int QualityScore,
    DateTimeOffset CapturedAtUtc,
    Guid AuditRef,
    bool RawPayloadStored,
    IReadOnlyList<string> Warnings)
{
    public static BridgeResponse Failed(string matchStatus, string message)
        => new(false, message, "LocalBridgeTemplate", "Garmetix Fingerprint Bridge", "", matchStatus, null, "", "", null, 0, DateTimeOffset.UtcNow, Guid.NewGuid(), false, []);
}

public interface IFingerprintVendorAdapter
{
    Task<BridgeResponse> HealthAsync(CancellationToken cancellationToken);
    Task<BridgeResponse> CaptureAsync(BridgeRequest request, CancellationToken cancellationToken);
    Task<BridgeResponse> IdentifyAsync(BridgeRequest request, CancellationToken cancellationToken);
    Task<BridgeResponse> EnrollAsync(BridgeRequest request, CancellationToken cancellationToken);
}

public sealed class SimulatorFingerprintVendorAdapter(IConfiguration configuration) : IFingerprintVendorAdapter
{
    private FingerprintBridgeOptions Options => configuration.GetSection("Bridge").Get<FingerprintBridgeOptions>() ?? new();

    public Task<BridgeResponse> HealthAsync(CancellationToken cancellationToken)
        => Task.FromResult(Create(true, "Health", "Healthy", new BridgeRequest(null, "SIM-EMP-001", "Simulator Employee", null, null, null, null, false),
            "Local fingerprint bridge template is running. Replace SimulatorFingerprintVendorAdapter with the selected vendor SDK adapter."));

    public Task<BridgeResponse> CaptureAsync(BridgeRequest request, CancellationToken cancellationToken)
        => Task.FromResult(Create(true, "Capture", "Captured", request, "Fingerprint capture handshake completed without returning raw biometric payload."));

    public Task<BridgeResponse> IdentifyAsync(BridgeRequest request, CancellationToken cancellationToken)
        => Task.FromResult(Create(true, "Identify", "Matched", request, "Fingerprint identify handshake matched the simulator employee reference."));

    public Task<BridgeResponse> EnrollAsync(BridgeRequest request, CancellationToken cancellationToken)
        => Task.FromResult(Create(true, "Enroll", "Enrolled", request, "Fingerprint enroll handshake created a simulator template reference."));

    private BridgeResponse Create(bool success, string operation, string matchStatus, BridgeRequest request, string message)
    {
        var options = Options;
        var employeeCode = string.IsNullOrWhiteSpace(request.EmployeeCode) ? "SIM-EMP-001" : request.EmployeeCode.Trim();
        var employeeName = string.IsNullOrWhiteSpace(request.EmployeeName) ? "Simulator Employee" : request.EmployeeName.Trim();
        var warnings = request.RawPayloadAllowed
            ? new[] { "rawPayloadAllowed was ignored. This bridge never returns raw fingerprint images, WSQ, minutiae or ISO templates." }
            : Array.Empty<string>();

        return new BridgeResponse(
            success,
            message,
            "LocalBridgeTemplate",
            options.Vendor,
            options.DeviceSerial,
            matchStatus,
            request.EmployeeId,
            employeeCode,
            employeeName,
            operation.Equals("Health", StringComparison.OrdinalIgnoreCase) ? null : $"local-template-ref-{employeeCode.ToLowerInvariant()}",
            Math.Clamp(options.QualityScore, 0, 100),
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            false,
            warnings);
    }
}

public sealed class MantraFingerprintVendorAdapter(IConfiguration configuration, IHttpClientFactory httpClientFactory) : IFingerprintVendorAdapter
{
    private FingerprintBridgeOptions Options => configuration.GetSection("Bridge").Get<FingerprintBridgeOptions>() ?? new();
    private static readonly HashSet<string> RawBiometricFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "rawImage",
        "fingerprintImage",
        "wsq",
        "minutiae",
        "isoTemplate",
        "templateBase64",
        "biometricPayload",
        "image",
        "template",
        "templateData"
    };

    public Task<BridgeResponse> HealthAsync(CancellationToken cancellationToken)
        => CallMantraAsync("Health", Options.MantraHealthPath, new BridgeRequest(null, "MANTRA-CHECK", "Mantra Device Check", null, null, null, null, false), true, cancellationToken);

    public Task<BridgeResponse> CaptureAsync(BridgeRequest request, CancellationToken cancellationToken)
        => CallMantraAsync("Capture", Options.MantraCapturePath, request, false, cancellationToken);

    public Task<BridgeResponse> IdentifyAsync(BridgeRequest request, CancellationToken cancellationToken)
        => CallMantraAsync("Identify", Options.MantraIdentifyPath, request, false, cancellationToken);

    public Task<BridgeResponse> EnrollAsync(BridgeRequest request, CancellationToken cancellationToken)
        => CallMantraAsync("Enroll", Options.MantraEnrollPath, request, false, cancellationToken);

    private async Task<BridgeResponse> CallMantraAsync(string operation, string operationPath, BridgeRequest request, bool healthCheck, CancellationToken cancellationToken)
    {
        var options = Options;
        var configured = TryBuildMantraUri(options, operationPath, out var uri, out var configurationMessage);
        if (!configured || uri is null)
        {
            return NotConfigured(operation, request, configurationMessage);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.MantraTimeoutSeconds, 3, 60)));

        try
        {
            var client = httpClientFactory.CreateClient("MantraFingerprintService");
            using var response = healthCheck
                ? await client.GetAsync(uri, timeout.Token)
                : await client.PostAsJsonAsync(uri, request with { RawPayloadAllowed = false }, timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                return FailedFromService(operation, request, "ServiceRejected", $"Mantra service returned HTTP {(int)response.StatusCode}.", body);
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                return FailedFromService(operation, request, "EmptyResponse", "Mantra service returned an empty response.", body);
            }

            using var json = JsonDocument.Parse(body);
            if (ContainsRawBiometricField(json.RootElement, out var blockedField))
            {
                return new BridgeResponse(
                    false,
                    $"Mantra service response was blocked because it contained raw biometric field '{blockedField}'.",
                    "MantraBridgeAdapter",
                    SafeString(ReadString(json.RootElement, "vendor"), "Mantra"),
                    SafeString(ReadString(json.RootElement, "deviceSerial"), SafeString(options.DeviceSerial, "MANTRA-SERVICE")),
                    "RawPayloadBlocked",
                    request.EmployeeId,
                    SafeEmployeeCode(request),
                    SafeEmployeeName(request),
                    null,
                    0,
                    DateTimeOffset.UtcNow,
                    Guid.NewGuid(),
                    false,
                    ["Raw biometric payloads are not allowed in bridge responses. Configure the Mantra service to return templateRef and audit metadata only."]);
            }

            return NormalizeServiceResponse(operation, request, json.RootElement, options);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return FailedFromService(operation, request, "ServiceTimeout", "Mantra service did not respond before the bridge timeout.", "");
        }
        catch (JsonException)
        {
            return FailedFromService(operation, request, "InvalidJson", "Mantra service returned invalid JSON.", "");
        }
        catch (HttpRequestException ex)
        {
            return FailedFromService(operation, request, "ServiceUnavailable", $"Mantra service is not reachable: {ex.Message}", "");
        }
    }

    private static BridgeResponse NormalizeServiceResponse(string operation, BridgeRequest request, JsonElement root, FingerprintBridgeOptions options)
    {
        var success = ReadBool(root, "success") ?? true;
        var message = SafeString(ReadString(root, "message"), $"{operation} completed through the configured Mantra service.");
        var matchStatus = SafeString(ReadString(root, "matchStatus"), success ? $"{operation}Accepted" : $"{operation}Failed");
        var qualityScore = Math.Clamp(ReadInt(root, "qualityScore") ?? ReadInt(root, "quality") ?? 0, 0, 100);
        var auditRef = ReadGuid(root, "auditRef") ?? ReadGuid(root, "auditId") ?? Guid.NewGuid();
        var rawPayloadStored = ReadBool(root, "rawPayloadStored") ?? false;
        var templateRef = operation.Equals("Health", StringComparison.OrdinalIgnoreCase)
            ? null
            : CleanOptionalString(ReadString(root, "templateRef") ?? ReadString(root, "templateReference") ?? ReadString(root, "referenceId"));

        if (rawPayloadStored)
        {
            success = false;
            matchStatus = "RawPayloadBlocked";
            templateRef = null;
            message = "Mantra service reported rawPayloadStored=true, so the response was rejected.";
        }

        return new BridgeResponse(
            success,
            message,
            "MantraBridgeAdapter",
            SafeString(ReadString(root, "vendor"), SafeString(options.Vendor, "Mantra")),
            SafeString(ReadString(root, "deviceSerial"), SafeString(options.DeviceSerial, "MANTRA-SERVICE")),
            matchStatus,
            ReadGuid(root, "employeeId") ?? request.EmployeeId,
            SafeString(ReadString(root, "employeeCode"), SafeEmployeeCode(request)),
            SafeString(ReadString(root, "employeeName"), SafeEmployeeName(request)),
            templateRef,
            qualityScore,
            ReadDateTimeOffset(root, "capturedAtUtc") ?? DateTimeOffset.UtcNow,
            auditRef,
            false,
            BuildWarnings(root, rawPayloadStored));
    }

    private static IReadOnlyList<string> BuildWarnings(JsonElement root, bool rawPayloadStored)
    {
        var warnings = new List<string>();
        if (root.TryGetProperty("warnings", out var value) && value.ValueKind == JsonValueKind.Array)
        {
            warnings.AddRange(value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!.Trim())
                .Take(5));
        }

        if (rawPayloadStored)
        {
            warnings.Add("rawPayloadStored=true is not allowed. The bridge rejected the service response.");
        }

        if (warnings.Count == 0)
        {
            warnings.Add("Mantra service response was normalized by the local bridge. Raw biometric payloads remain blocked.");
        }

        return warnings;
    }

    private static BridgeResponse FailedFromService(string operation, BridgeRequest request, string matchStatus, string message, string responseBody)
    {
        var warnings = string.IsNullOrWhiteSpace(responseBody)
            ? new[] { "No raw biometric payload was stored or returned by the bridge." }
            : ["Service response body was not forwarded to Garmetix to avoid leaking vendor payloads."];

        return new BridgeResponse(
            false,
            message,
            "MantraBridgeAdapter",
            "Mantra",
            "MANTRA-SERVICE",
            matchStatus,
            request.EmployeeId,
            SafeEmployeeCode(request),
            SafeEmployeeName(request),
            null,
            0,
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            false,
            warnings);
    }

    private BridgeResponse NotConfigured(string operation, BridgeRequest request, string? configurationMessage = null)
    {
        var options = Options;
        var warnings = new[]
        {
            "Mantra adapter boundary is selected but the official Mantra SDK/service is not wired yet.",
            configurationMessage ?? "Install the Mantra SDK/service on the kiosk host, then set Bridge:MantraServiceUrl to its local service URL.",
            "Return only templateRef, matchStatus, qualityScore, auditRef and device metadata. Raw biometric payloads remain blocked."
        };

        return new BridgeResponse(
            false,
            $"{operation} cannot run until the Mantra SDK/service is configured on this bridge host.",
            "MantraBridgeAdapter",
            string.IsNullOrWhiteSpace(options.Vendor) ? "Mantra" : options.Vendor,
            string.IsNullOrWhiteSpace(options.DeviceSerial) ? "MANTRA-NOT-CONFIGURED" : options.DeviceSerial,
            "SdkNotConfigured",
            request.EmployeeId,
            SafeEmployeeCode(request),
            SafeEmployeeName(request),
            null,
            0,
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            false,
            warnings);
    }

    private static bool TryBuildMantraUri(FingerprintBridgeOptions options, string operationPath, out Uri? uri, out string message)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(options.MantraServiceUrl))
        {
            message = "Bridge:MantraServiceUrl is blank.";
            return false;
        }

        if (!Uri.TryCreate(options.MantraServiceUrl.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            message = "Bridge:MantraServiceUrl must be an absolute http or https localhost/private-LAN URL.";
            return false;
        }

        if (!IsAllowedMantraServiceHost(baseUri.Host))
        {
            message = "Bridge:MantraServiceUrl must point to localhost, host.docker.internal or a private LAN address.";
            return false;
        }

        var path = string.IsNullOrWhiteSpace(operationPath) ? "/" : operationPath.Trim();
        uri = new Uri(baseUri, path.TrimStart('/'));
        message = "";
        return true;
    }

    private static bool IsAllowedMantraServiceHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("host.docker.internal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(host, out var address))
        {
            return false;
        }

        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        var bytes = address.GetAddressBytes();
        return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            && (bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168));
    }

    private static bool ContainsRawBiometricField(JsonElement element, out string blockedField)
    {
        blockedField = "";
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (RawBiometricFieldNames.Contains(property.Name))
                {
                    blockedField = property.Name;
                    return true;
                }

                if (ContainsRawBiometricField(property.Value, out blockedField))
                {
                    return true;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (ContainsRawBiometricField(item, out blockedField))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string? ReadString(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool? ReadBool(JsonElement root, string name)
        => root.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
                _ => null
            }
            : null;

    private static int? ReadInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static Guid? ReadGuid(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var parsed)
            ? parsed
            : null;

    private static DateTimeOffset? ReadDateTimeOffset(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), out var parsed)
            ? parsed
            : null;

    private static string SafeString(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string? CleanOptionalString(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string SafeEmployeeCode(BridgeRequest request)
        => string.IsNullOrWhiteSpace(request.EmployeeCode) ? "MANTRA-EMP" : request.EmployeeCode.Trim();

    private static string SafeEmployeeName(BridgeRequest request)
        => string.IsNullOrWhiteSpace(request.EmployeeName) ? "Mantra Enrollment Employee" : request.EmployeeName.Trim();
}

using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls(builder.Configuration["Bridge:Urls"] ?? "http://127.0.0.1:8787");
builder.Services.Configure<FingerprintBridgeOptions>(builder.Configuration.GetSection("Bridge"));
builder.Services.AddSingleton<IFingerprintVendorAdapter>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var options = configuration.GetSection("Bridge").Get<FingerprintBridgeOptions>() ?? new();
    return options.Adapter.Equals("Mantra", StringComparison.OrdinalIgnoreCase)
        ? new MantraFingerprintVendorAdapter(configuration)
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

public sealed class MantraFingerprintVendorAdapter(IConfiguration configuration) : IFingerprintVendorAdapter
{
    private FingerprintBridgeOptions Options => configuration.GetSection("Bridge").Get<FingerprintBridgeOptions>() ?? new();

    public Task<BridgeResponse> HealthAsync(CancellationToken cancellationToken)
        => Task.FromResult(NotConfigured("Health", new BridgeRequest(null, "MANTRA-CHECK", "Mantra Device Check", null, null, null, null, false)));

    public Task<BridgeResponse> CaptureAsync(BridgeRequest request, CancellationToken cancellationToken)
        => Task.FromResult(NotConfigured("Capture", request));

    public Task<BridgeResponse> IdentifyAsync(BridgeRequest request, CancellationToken cancellationToken)
        => Task.FromResult(NotConfigured("Identify", request));

    public Task<BridgeResponse> EnrollAsync(BridgeRequest request, CancellationToken cancellationToken)
        => Task.FromResult(NotConfigured("Enroll", request));

    private BridgeResponse NotConfigured(string operation, BridgeRequest request)
    {
        var options = Options;
        var employeeCode = string.IsNullOrWhiteSpace(request.EmployeeCode) ? "MANTRA-EMP" : request.EmployeeCode.Trim();
        var employeeName = string.IsNullOrWhiteSpace(request.EmployeeName) ? "Mantra Enrollment Employee" : request.EmployeeName.Trim();
        var warnings = new[]
        {
            "Mantra adapter boundary is selected but the official Mantra SDK/service is not wired yet.",
            "Install the Mantra SDK/service on the kiosk host, then implement SDK calls inside MantraFingerprintVendorAdapter.",
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
            employeeCode,
            employeeName,
            null,
            0,
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            false,
            warnings);
    }
}

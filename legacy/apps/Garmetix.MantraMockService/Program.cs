using System.Net;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["MockMantra:Urls"] ?? "http://127.0.0.1:8788");

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
        await context.Response.WriteAsJsonAsync(new { success = false, message = "Mock Mantra service accepts local or private LAN callers only.", rawPayloadStored = false });
        return;
    }

    context.Response.Headers.CacheControl = "no-store";
    await next();
});

app.MapGet("/health", () => Safe("Health", "Healthy", new MockMantraRequest(null, "MANTRA-CHECK", "Mantra Device Check", false)));
app.MapPost("/capture", (MockMantraRequest request) => Safe("Capture", "Captured", request));
app.MapPost("/identify", (MockMantraRequest request) => Safe("Identify", "Matched", request));
app.MapPost("/enroll", (MockMantraRequest request) => Safe("Enroll", "Enrolled", request));

app.MapPost("/unsafe/enroll-with-raw", (MockMantraRequest request) => Results.Ok(new
{
    success = true,
    message = "Unsafe mock response intentionally includes raw biometric-looking fields for bridge rejection testing.",
    vendor = "Mantra Mock Unsafe",
    deviceSerial = "MANTRA-MOCK-UNSAFE",
    matchStatus = "Enrolled",
    employeeId = request.EmployeeId,
    employeeCode = SafeEmployeeCode(request),
    employeeName = SafeEmployeeName(request),
    templateRef = $"mantra-mock-ref-{SafeEmployeeCode(request).ToLowerInvariant()}",
    qualityScore = 88,
    capturedAtUtc = DateTimeOffset.UtcNow,
    auditRef = Guid.NewGuid(),
    rawPayloadStored = false,
    rawImage = "blocked-test-payload"
}));

app.Run();

static object Safe(string operation, string matchStatus, MockMantraRequest request)
{
    var employeeCode = SafeEmployeeCode(request);
    return new
    {
        success = true,
        message = $"{operation} completed by mock Mantra service.",
        vendor = "Mantra Mock Service",
        deviceSerial = "MANTRA-MOCK-001",
        matchStatus,
        employeeId = request.EmployeeId,
        employeeCode,
        employeeName = SafeEmployeeName(request),
        templateRef = operation.Equals("Health", StringComparison.OrdinalIgnoreCase) ? null : $"mantra-mock-ref-{employeeCode.ToLowerInvariant()}",
        qualityScore = operation.Equals("Health", StringComparison.OrdinalIgnoreCase) ? 0 : 89,
        capturedAtUtc = DateTimeOffset.UtcNow,
        auditRef = Guid.NewGuid(),
        rawPayloadStored = false,
        warnings = new[] { "Mock service only returns safe references. It does not capture real fingerprints." }
    };
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

static string SafeEmployeeCode(MockMantraRequest request)
    => string.IsNullOrWhiteSpace(request.EmployeeCode) ? "MANTRA-MOCK-EMP" : request.EmployeeCode.Trim();

static string SafeEmployeeName(MockMantraRequest request)
    => string.IsNullOrWhiteSpace(request.EmployeeName) ? "Mantra Mock Employee" : request.EmployeeName.Trim();

public sealed record MockMantraRequest(Guid? EmployeeId, string? EmployeeCode, string? EmployeeName, bool RawPayloadAllowed);

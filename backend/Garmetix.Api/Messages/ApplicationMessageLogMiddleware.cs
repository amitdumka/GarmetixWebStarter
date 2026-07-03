using System.Diagnostics;
using System.Security.Claims;
using Garmetix.Api.Workspace;

namespace Garmetix.Api.Messages;

public sealed class ApplicationMessageLogMiddleware(
    RequestDelegate next,
    IServiceScopeFactory scopeFactory,
    ILogger<ApplicationMessageLogMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (ShouldSkip(context.Request))
        {
            await next(context);
            return;
        }

        var startedAt = Stopwatch.GetTimestamp();
        var operationId = Guid.NewGuid();

        try
        {
            await next(context);

            if (ShouldLogResponse(context.Request, context.Response.StatusCode))
            {
                var success = context.Response.StatusCode < StatusCodes.Status400BadRequest;
                await PersistAsync(
                    context,
                    operationId,
                    success ? ApplicationMessageLogService.LevelSuccess : LevelForStatus(context.Response.StatusCode),
                    success ? "HttpWriteCompleted" : "HttpRequestFailed",
                    $"{context.Request.Method} {context.Request.Path} completed with HTTP {context.Response.StatusCode}.",
                    new
                    {
                        traceId = context.TraceIdentifier,
                        method = context.Request.Method,
                        path = context.Request.Path.Value,
                        statusCode = context.Response.StatusCode,
                        elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds
                    },
                    success);
            }
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await PersistAsync(
                context,
                operationId,
                ApplicationMessageLogService.LevelError,
                "UnhandledException",
                $"{context.Request.Method} {context.Request.Path} failed with an unhandled server exception.",
                new
                {
                    traceId = context.TraceIdentifier,
                    method = context.Request.Method,
                    path = context.Request.Path.Value,
                    elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                    exceptionType = exception.GetType().FullName,
                    exception.Message,
                    exception.StackTrace,
                    innerException = exception.InnerException is null
                        ? null
                        : new
                        {
                            exception.InnerException.GetType().FullName,
                            exception.InnerException.Message,
                            exception.InnerException.StackTrace
                        }
                },
                false);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new
            {
                message = "Unexpected server error. Details were saved in Message Logs.",
                operationId,
                traceId = context.TraceIdentifier
            });
        }
    }

    private async Task PersistAsync(
        HttpContext context,
        Guid operationId,
        string level,
        string eventName,
        string message,
        object details,
        bool success)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var logs = scope.ServiceProvider.GetRequiredService<ApplicationMessageLogService>();
            await logs.WriteAsync(new ApplicationMessageLogCreateRequest(
                level,
                "API",
                eventName,
                message,
                ApplicationMessageLogService.SerializeDetails(details),
                WorkspaceScope.ClaimGuid(context, "companyId"),
                WorkspaceScope.ClaimGuid(context, "storeGroupId"),
                WorkspaceScope.ClaimGuid(context, "storeId"),
                ClaimGuid(context.User, ClaimTypes.NameIdentifier),
                context.User.Identity?.Name ?? context.User.FindFirst(ClaimTypes.Name)?.Value,
                $"{context.Request.Method} {context.Request.Path}",
                operationId,
                success));
        }
        catch (Exception loggingException)
        {
            logger.LogError(
                loggingException,
                "Could not persist application message log for {Method} {Path}.",
                context.Request.Method,
                context.Request.Path);
        }
    }

    private static bool ShouldSkip(HttpRequest request)
        => HttpMethods.IsOptions(request.Method)
            || request.Path.StartsWithSegments("/api/message-logs")
            || request.Path.StartsWithSegments("/api/notifications");

    private static bool ShouldLogResponse(HttpRequest request, int statusCode)
        => statusCode >= StatusCodes.Status400BadRequest
            || HttpMethods.IsPost(request.Method)
            || HttpMethods.IsPut(request.Method)
            || HttpMethods.IsPatch(request.Method)
            || HttpMethods.IsDelete(request.Method);

    private static string LevelForStatus(int statusCode)
        => statusCode >= StatusCodes.Status500InternalServerError
            ? ApplicationMessageLogService.LevelError
            : ApplicationMessageLogService.LevelWarning;

    private static Guid? ClaimGuid(ClaimsPrincipal principal, string claimType)
        => Guid.TryParse(principal.FindFirst(claimType)?.Value, out var value) ? value : null;
}

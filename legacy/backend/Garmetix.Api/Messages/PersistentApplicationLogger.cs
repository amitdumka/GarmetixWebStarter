using System.Threading.Channels;

namespace Garmetix.Api.Messages;

public sealed class PersistentApplicationLogQueue
{
    private readonly Channel<ApplicationMessageLogCreateRequest> channel =
        Channel.CreateBounded<ApplicationMessageLogCreateRequest>(new BoundedChannelOptions(2000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    public bool TryWrite(ApplicationMessageLogCreateRequest entry) => channel.Writer.TryWrite(entry);

    public IAsyncEnumerable<ApplicationMessageLogCreateRequest> ReadAllAsync(CancellationToken cancellationToken)
        => channel.Reader.ReadAllAsync(cancellationToken);
}

public sealed class PersistentApplicationLoggerProvider(PersistentApplicationLogQueue queue) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new PersistentApplicationLogger(categoryName, queue);

    public void Dispose()
    {
    }

    private sealed class PersistentApplicationLogger(
        string categoryName,
        PersistentApplicationLogQueue queue) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;

        public bool IsEnabled(LogLevel logLevel)
            => logLevel >= LogLevel.Information
                && IsApplicationCategory(categoryName);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            if (string.IsNullOrWhiteSpace(message) && exception is null)
            {
                return;
            }

            var success = logLevel < LogLevel.Warning;
            queue.TryWrite(new ApplicationMessageLogCreateRequest(
                Level(logLevel),
                "Runtime",
                categoryName,
                string.IsNullOrWhiteSpace(message) ? exception?.Message ?? "Application log message." : message,
                ApplicationMessageLogService.SerializeDetails(new
                {
                    eventId = eventId.Id,
                    eventName = eventId.Name,
                    logLevel = logLevel.ToString(),
                    exceptionType = exception?.GetType().FullName,
                    exception?.Message,
                    exception?.StackTrace,
                    innerException = exception?.InnerException is null
                        ? null
                        : new
                        {
                            exception.InnerException.GetType().FullName,
                            exception.InnerException.Message,
                            exception.InnerException.StackTrace
                        }
                }),
                Resource: categoryName,
                Success: success));
        }

        private static bool IsApplicationCategory(string category)
            => !category.StartsWith("Garmetix.Api.Messages.", StringComparison.Ordinal)
                && (category.StartsWith("Garmetix.", StringComparison.Ordinal)
                    || category.StartsWith("Database", StringComparison.Ordinal)
                    || string.Equals(category, "Program", StringComparison.Ordinal));

        private static string Level(LogLevel logLevel)
            => logLevel switch
            {
                LogLevel.Warning => ApplicationMessageLogService.LevelWarning,
                LogLevel.Error or LogLevel.Critical => ApplicationMessageLogService.LevelError,
                _ => ApplicationMessageLogService.LevelInfo
            };
    }

    private sealed class NoopScope : IDisposable
    {
        public static NoopScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}

public sealed class PersistentApplicationLogHostedService(
    PersistentApplicationLogQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<PersistentApplicationLogHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var entry in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var logs = scope.ServiceProvider.GetRequiredService<ApplicationMessageLogService>();
                await logs.WriteAsync(entry, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not persist queued application log {EventName}.", entry.EventName);
            }
        }
    }
}

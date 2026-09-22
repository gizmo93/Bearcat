using Microsoft.Extensions.Logging;

namespace Bearcat.Infrastructure.Logging;

[ProviderAlias("LogStream")]
public sealed class LogStreamLoggerProvider(LogStreamBroadcaster broadcaster) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new LogStreamLogger(broadcaster, categoryName);
    }

    public void Dispose() { }

    private sealed class LogStreamLogger(LogStreamBroadcaster broadcaster, string categoryName)
        : ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            broadcaster.Publish(
                new LogLine(
                    DateTimeOffset.Now,
                    logLevel,
                    categoryName,
                    formatter(state, exception),
                    exception?.ToString()
                )
            );
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose() { }
    }
}

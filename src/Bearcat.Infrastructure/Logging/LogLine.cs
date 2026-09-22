using Microsoft.Extensions.Logging;

namespace Bearcat.Infrastructure.Logging;

public record LogLine(
    DateTimeOffset Timestamp,
    LogLevel Level,
    string Category,
    string Message,
    string? Exception
);

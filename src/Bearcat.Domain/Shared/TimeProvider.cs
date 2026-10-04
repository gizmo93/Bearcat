using Microsoft.Extensions.Configuration;

namespace Bearcat.Domain.Shared;

public class TimeProvider(IConfiguration configuration)
{
    private readonly TimeZoneInfo localTimeZone = FindLocalTimeZone(configuration);

    public virtual DateTime GetLocalNow()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, localTimeZone);
    }

    public virtual DateTimeOffset ConvertToLocalTime(DateTimeOffset value)
    {
        return TimeZoneInfo.ConvertTime(value, localTimeZone);
    }

    private static TimeZoneInfo FindLocalTimeZone(IConfiguration configuration)
    {
        var configuredTimeZoneId = configuration.GetSection("LocalTimezone").Value;

        return string.IsNullOrWhiteSpace(configuredTimeZoneId)
            ? TimeZoneInfo.Local
            : TimeZoneInfo.FindSystemTimeZoneById(configuredTimeZoneId);
    }
}

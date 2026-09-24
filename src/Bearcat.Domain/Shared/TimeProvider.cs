using Microsoft.Extensions.Configuration;

namespace Bearcat.Domain.Shared;

public class TimeProvider(IConfiguration configuration)
{
    private readonly TimeZoneInfo localTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
        configuration.GetSection("LocalTimezone").Value ?? "UTC"
    );

    public virtual DateTime GetLocalNow()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, localTimeZone);
    }
}

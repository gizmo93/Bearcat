using Microsoft.Extensions.Configuration;

namespace Bearcat.Domain.Shared;

public class TimeProvider(IConfiguration configuration)
{
    private readonly TimeZoneInfo localTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
        configuration.GetValue<string>("LocalTimezone") ?? "UTC");

    public DateTime GetLocalNow()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, localTimeZone);
    }
}

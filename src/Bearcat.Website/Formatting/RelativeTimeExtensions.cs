using Humanizer;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Website.Formatting;

public static class RelativeTimeExtensions
{
    public static string Humanize(this TimeProvider timeProvider, DateTime value)
    {
        var elapsed = timeProvider.GetLocalNow() - value;

        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        var nowUtc = DateTime.UtcNow;
        return (nowUtc - elapsed).Humanize(utcDate: true, dateToCompareAgainst: nowUtc);
    }
}

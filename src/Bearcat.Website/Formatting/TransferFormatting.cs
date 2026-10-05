using System.Globalization;
using Bearcat.Domain.Shared.Transfers;
using Humanizer;

namespace Bearcat.Website.Formatting;

public static class TransferFormatting
{
    public static string FormatBytes(long bytes)
    {
        return bytes <= 0 ? "0 B" : bytes.Bytes().Humanize("0.0", CultureInfo.CurrentCulture);
    }

    public static string? FormatSpeed(double bytesPerSecond)
    {
        return bytesPerSecond <= 0
            ? null
            : $"{bytesPerSecond.Bytes().Humanize("0.0", CultureInfo.CurrentCulture)}/s";
    }

    public static string? FormatRemainingTime(TransferProgressSnapshot snapshot)
    {
        var remainingBytes = snapshot.TotalBytes - snapshot.TransferredBytes;

        if (snapshot.BytesPerSecond <= 0 || snapshot.TotalBytes <= 0 || remainingBytes <= 0)
        {
            return null;
        }

        var remainingTime = TimeSpan.FromSeconds(
            Math.Ceiling(remainingBytes / snapshot.BytesPerSecond)
        );

        return remainingTime.Humanize(
            precision: 1,
            culture: CultureInfo.CurrentCulture,
            maxUnit: TimeUnit.Day,
            minUnit: TimeUnit.Second
        );
    }
}

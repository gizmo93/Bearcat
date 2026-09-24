using Humanizer;

namespace Bearcat.Website.Formatting;

public static class TransferFormatting
{
    public static string FormatBytes(long bytes)
    {
        return bytes <= 0 ? "0 B" : bytes.Bytes().Humanize("0.0");
    }

    public static string? FormatSpeed(double bytesPerSecond)
    {
        return bytesPerSecond <= 0 ? null : $"{bytesPerSecond.Bytes().Humanize("0.0")}/s";
    }
}

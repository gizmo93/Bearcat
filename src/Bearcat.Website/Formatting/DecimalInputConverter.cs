using System.Globalization;

namespace Bearcat.Website.Formatting;

public static class DecimalInputConverter
{
    public static bool TryParse(string? input, out decimal value)
    {
        return decimal.TryParse(
            input?.Replace(',', '.'),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value
        );
    }

    public static string? FormatForInput(decimal? value)
    {
        return value?.ToString("0.###", CultureInfo.CurrentCulture);
    }

    public static string FormatMegabytesPerSecond(decimal megabytesPerSecond)
    {
        return $"{megabytesPerSecond.ToString("0.###", CultureInfo.CurrentCulture)} MB/s";
    }
}

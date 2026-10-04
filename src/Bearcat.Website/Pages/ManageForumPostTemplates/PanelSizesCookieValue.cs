using System.Globalization;

namespace Bearcat.Website.Pages.ManageForumPostTemplates;

public static class PanelSizesCookieValue
{
    private const double AllowedTotalDeviation = 1;

    public static string Format(IReadOnlyList<double> sizes)
    {
        return string.Join(
            ",",
            sizes.Select(size => size.ToString("0.##", CultureInfo.InvariantCulture))
        );
    }

    public static IReadOnlyList<double>? Parse(string? value, int expectedPanelCount)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split(',');
        if (parts.Length != expectedPanelCount)
        {
            return null;
        }

        var sizes = new List<double>(parts.Length);
        foreach (var part in parts)
        {
            if (
                !double.TryParse(
                    part,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var size
                ) || size is < 0 or > 100
            )
            {
                return null;
            }

            sizes.Add(size);
        }

        return Math.Abs(sizes.Sum() - 100) <= AllowedTotalDeviation ? sizes : null;
    }
}

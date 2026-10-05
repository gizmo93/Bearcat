using System.Globalization;

namespace Bearcat.Website.Pages.Home.Summary;

public static class SparklinePointsBuilder
{
    public const double Width = 100;

    public const double Height = 32;

    private const double VerticalPadding = 2;

    public static SparklinePoints? Build(IReadOnlyList<double> samples, int capacity)
    {
        if (samples.Count < 2)
        {
            return null;
        }

        var horizontalStep = Width / (capacity - 1);
        var firstX = Width - (samples.Count - 1) * horizontalStep;
        var maximum = samples.Max();

        var points = samples
            .Select(
                (sample, index) =>
                    FormatPoint(firstX + index * horizontalStep, GetY(sample, maximum))
            )
            .ToList();

        var linePoints = string.Join(" ", points);
        var areaPoints = $"{linePoints} {FormatPoint(Width, Height)} {FormatPoint(firstX, Height)}";

        return new SparklinePoints(linePoints, areaPoints);
    }

    private static double GetY(double sample, double maximum)
    {
        var ratio = maximum <= 0 ? 0 : sample / maximum;
        return Height - VerticalPadding - ratio * (Height - 2 * VerticalPadding);
    }

    private static string FormatPoint(double x, double y)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{x:0.##},{y:0.##}");
    }
}

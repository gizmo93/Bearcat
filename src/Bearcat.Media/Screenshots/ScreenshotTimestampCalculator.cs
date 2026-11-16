namespace Bearcat.Media.Screenshots;

public static class ScreenshotTimestampCalculator
{
    private const double StartFraction = 0.05;
    private const double EndFraction = 0.95;

    public static List<TimeSpan> Calculate(TimeSpan duration, int count)
    {
        var rangeStart = duration * StartFraction;
        var slotLength = duration * (EndFraction - StartFraction) / count;

        return Enumerable
            .Range(0, count)
            .Select(slotIndex => rangeStart + slotLength * (slotIndex + 0.5))
            .ToList();
    }
}

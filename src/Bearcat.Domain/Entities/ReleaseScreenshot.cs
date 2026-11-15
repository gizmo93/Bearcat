using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class ReleaseScreenshot
{
    public int Id { get; set; }

    public int ReleaseId { get; set; }

    public Release Release { get; set; } = null!;

    public ReleaseScreenshotKind Kind { get; set; }

    public int Index { get; set; }

    public TimeSpan? Timestamp { get; set; }

    public string RelativeFilePath { get; set; } = null!;

    public int Width { get; set; }

    public int Height { get; set; }

    public long SizeBytes { get; set; }

    public DateTime CreatedAt { get; set; }
}

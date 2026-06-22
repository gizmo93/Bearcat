namespace Bearcat.Domain.Entities;

public class ReleaseMediaFile
{
    public int Id { get; set; }

    public int ReleaseId { get; set; }

    public Release Release { get; set; } = null!;

    public string RelativePath { get; set; } = null!;

    public long SizeBytes { get; set; }

    public string MediaInfoJson { get; set; } = null!;

    public string MediaInfoText { get; set; } = null!;
}

namespace Bearcat.Domain.Entities;

public class ReleaseGroup
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public bool EnableAutomaticReuploads { get; set; }

    public int NumberOfHoursUntilReupload { get; set; }

    public int? QualityProfileId { get; set; }

    public QualityProfile? QualityProfile { get; set; }

    public List<Release> Releases { get; set; } = null!;
}

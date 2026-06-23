namespace Bearcat.Domain.Entities;

public class QualityProfile
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public List<QualityCheckRule> Rules { get; set; } = [];

    public List<ReleaseGroup> ReleaseGroups { get; set; } = [];
}

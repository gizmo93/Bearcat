namespace Bearcat.Domain.Entities;

public class HosterAccountPool
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string HosterClassName { get; set; }

    public long MinimumFreeSpaceBytes { get; set; }

    public List<HosterAccountPoolMember> Members { get; set; } = [];

    public List<UploadConfig> UploadConfigs { get; set; } = null!;

    public List<UploadConfigTemplate> UploadConfigTemplates { get; set; } = null!;
}

using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class ReleaseTemplate
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public ReleaseType ReleaseType { get; set; }

    public int ReleaseGroupId { get; set; }

    public ReleaseGroup ReleaseGroup { get; set; } = null!;

    public List<ArchiveConfigTemplate> ArchiveConfigTemplates { get; set; } = [];

    public List<UploadConfigTemplate> UploadConfigTemplates { get; set; } = [];
}

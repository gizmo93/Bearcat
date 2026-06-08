using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class ReleaseTemplate
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public ReleaseType ReleaseType { get; set; }

    public int ReleaseGroupId { get; set; }

    public ReleaseGroup ReleaseGroup { get; set; } = null!;

    public ReleaseCollectionDetectionMode ReleaseCollectionDetectionMode { get; set; } =
        ReleaseCollectionDetectionMode.Disabled;

    public string? ReleaseCollectionPattern { get; set; }

    public string? ReleaseCollectionKeyTemplate { get; set; }

    public string? ReleaseCollectionNameTemplate { get; set; }

    public List<ArchiveConfigTemplate> ArchiveConfigTemplates { get; set; } = [];

    public List<UploadConfigTemplate> UploadConfigTemplates { get; set; } = [];

    public List<ImageUploadConfigTemplate> ImageUploadConfigTemplates { get; set; } = [];
}

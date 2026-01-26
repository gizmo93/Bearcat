using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class Release
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public ReleaseType ReleaseType { get; set; }

    public List<UploadConfig> UploadConfigs { get; set; } = null!;

    public List<ArchiveConfig> ArchiveConfigs { get; set; } = null!;

    public string ReleaseFolderPath { get; set; } = null!;
}

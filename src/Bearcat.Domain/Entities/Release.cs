using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class Release
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public ReleaseType ReleaseType { get; set; }

    public int ReleaseGroupId { get; set; }

    public ReleaseGroup ReleaseGroup { get; set; } = null!;

    public List<UploadConfig> UploadConfigs { get; set; } = null!;

    public List<ArchiveConfig> ArchiveConfigs { get; set; } = null!;

    public string ReleaseFolderPath { get; set; } = null!;

    public ReleaseInfo? ReleaseInfo { get; set; }

    public DateTime? ReleaseInfoCheckedAt { get; set; }
}

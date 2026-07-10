using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class Release
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public ReleaseType ReleaseType { get; set; }

    public ReleaseContentType ReleaseContentType { get; set; }

    public string? PrimaryLanguageCode { get; set; }

    public int ReleaseGroupId { get; set; }

    public ReleaseGroup ReleaseGroup { get; set; } = null!;

    public int? ReleaseCollectionId { get; set; }

    public ReleaseCollection? ReleaseCollection { get; set; }

    public List<UploadConfig> UploadConfigs { get; set; } = null!;

    public List<ImageUploadConfig> ImageUploadConfigs { get; set; } = [];

    public List<PostedLocation> PostedLocations { get; set; } = [];

    public List<ArchiveConfig> ArchiveConfigs { get; set; } = null!;

    public string? ReleaseFolderPath { get; set; }

    public ReleaseInfo? ReleaseInfo { get; set; }

    public ReleaseNfo? ReleaseNfo { get; set; }

    public List<ReleaseExternalIdentifier> ExternalIdentifiers { get; set; } = [];

    public DateTime? ReleaseInfoCheckedAt { get; set; }

    public List<ReleaseMediaFile> MediaFiles { get; set; } = [];

    public DateTime? MediaMetadataExtractedAt { get; set; }

    public DateTime? UploadsPostedAt { get; set; }

    public QualityGateState QualityGateState { get; set; } = QualityGateState.NotEvaluated;

    public DateTime? QualityGateEvaluatedAt { get; set; }

    public List<ReleaseQualityIssue> QualityIssues { get; set; } = [];

    public List<Notification> Notifications { get; set; } = [];
}

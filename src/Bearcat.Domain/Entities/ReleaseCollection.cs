using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class ReleaseCollection
{
    public int Id { get; set; }

    public ReleaseContentType ReleaseContentType { get; set; }

    public int ReleaseGroupId { get; set; }

    public ReleaseGroup ReleaseGroup { get; set; } = null!;

    public string Key { get; set; } = null!;

    public string Name { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public List<Release> Releases { get; set; } = [];

    public List<CollectionUploadSlot> UploadSlots { get; set; } = [];

    public List<ImageUploadConfig> ImageUploadConfigs { get; set; } = [];

    public ReleaseCollectionMetadata? Metadata { get; set; }

    public DateTime? MetadataCheckedAt { get; set; }
}

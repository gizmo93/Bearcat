using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class CollectionUploadSlot
{
    public int Id { get; set; }

    public int ReleaseCollectionId { get; set; }

    public ReleaseCollection ReleaseCollection { get; set; } = null!;

    public string Key { get; set; } = null!;

    public string Name { get; set; } = null!;

    public bool IsRequired { get; set; }

    public CollectionUploadSlotPasswordPolicy PasswordPolicy { get; set; } =
        CollectionUploadSlotPasswordPolicy.Ignore;

    public string? ExpectedArchivePassword { get; set; }

    public List<UploadConfig> UploadConfigs { get; set; } = [];
}

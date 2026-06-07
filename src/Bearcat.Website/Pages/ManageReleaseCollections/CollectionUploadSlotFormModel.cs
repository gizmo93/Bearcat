using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.ManageReleaseCollections;

public class CollectionUploadSlotFormModel
{
    public int ReleaseCollectionId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? HosterRegistrationId { get; set; }

    public string ArchiveConfigName { get; set; } = string.Empty;

    public bool PremiumOnlyDownload { get; set; }

    public bool IsRequired { get; set; }

    public CollectionUploadSlotPasswordPolicy PasswordPolicy { get; set; } =
        CollectionUploadSlotPasswordPolicy.Ignore;

    public string? ExpectedArchivePassword { get; set; }
}

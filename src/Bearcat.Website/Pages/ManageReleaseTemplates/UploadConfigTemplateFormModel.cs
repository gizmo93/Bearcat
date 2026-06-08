using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.ManageReleaseTemplates;

public class UploadConfigTemplateFormModel
{
    public string? Name { get; set; }

    public int? HosterRegistrationId { get; set; }

    public int? ArchiveConfigTemplateId { get; set; }

    public bool PremiumOnlyDownload { get; set; }

    public string? CollectionUploadSlotKey { get; set; }

    public string? CollectionUploadSlotName { get; set; }

    public bool CollectionUploadSlotIsRequired { get; set; }

    public CollectionUploadSlotPasswordPolicy CollectionUploadSlotPasswordPolicy { get; set; } =
        CollectionUploadSlotPasswordPolicy.Ignore;

    public string? CollectionUploadSlotExpectedArchivePassword { get; set; }

    public List<string> LinksDistributedTo { get; set; } = [];

    public bool IsEdit { get; set; }
}

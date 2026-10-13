using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class UploadConfigTemplate
{
    public int Id { get; set; }

    public int ReleaseTemplateId { get; set; }

    public ReleaseTemplate ReleaseTemplate { get; set; } = null!;

    public int ArchiveConfigTemplateId { get; set; }

    public ArchiveConfigTemplate ArchiveConfigTemplate { get; set; } = null!;

    public int? HosterRegistrationId { get; set; }

    public HosterRegistration? HosterRegistration { get; set; }

    public int? HosterAccountPoolId { get; set; }

    public HosterAccountPool? HosterAccountPool { get; set; }

    public string? Name { get; set; }

    public bool PremiumOnlyDownload { get; set; }

    public string? CollectionUploadSlotKey { get; set; }

    public string? CollectionUploadSlotName { get; set; }

    public bool CollectionUploadSlotIsRequired { get; set; }

    public CollectionUploadSlotPasswordPolicy CollectionUploadSlotPasswordPolicy { get; set; } =
        CollectionUploadSlotPasswordPolicy.Ignore;

    public string? CollectionUploadSlotExpectedArchivePassword { get; set; }

    public List<UploadConfigLinkCrypterTemplate> LinkCrypterTemplates { get; set; } = [];

    public string GetUploadTargetName() =>
        HosterAccountPoolId is null ? HosterRegistration!.Name : HosterAccountPool!.Name;
}

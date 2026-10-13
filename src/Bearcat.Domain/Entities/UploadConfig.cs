namespace Bearcat.Domain.Entities;

public class UploadConfig
{
    public int Id { get; set; }

    public int ReleaseId { get; set; }

    public Release Release { get; set; } = null!;

    public int? HosterRegistrationId { get; set; }

    public int? HosterAccountPoolId { get; set; }

    public int ArchiveConfigId { get; set; }

    public ArchiveConfig ArchiveConfig { get; set; } = null!;

    public int? CollectionUploadSlotId { get; set; }

    public CollectionUploadSlot? CollectionUploadSlot { get; set; }

    public HosterRegistration? HosterRegistration { get; set; }

    public HosterAccountPool? HosterAccountPool { get; set; }

    public string Name { get; set; } = null!;

    public bool PremiumOnlyDownload { get; set; }

    public List<Upload> Uploads { get; set; } = null!;

    public List<UploadConfigLinkCrypter> LinkCrypters { get; set; } = null!;

    public string GetHosterClassName() =>
        HosterAccountPoolId is null
            ? HosterRegistration!.HosterClassName
            : HosterAccountPool!.HosterClassName;

    public string GetUploadTargetName() =>
        HosterAccountPoolId is null ? HosterRegistration!.Name : HosterAccountPool!.Name;
}

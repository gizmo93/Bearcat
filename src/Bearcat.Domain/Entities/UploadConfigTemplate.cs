namespace Bearcat.Domain.Entities;

public class UploadConfigTemplate
{
    public int Id { get; set; }

    public int ReleaseTemplateId { get; set; }

    public ReleaseTemplate ReleaseTemplate { get; set; } = null!;

    public int ArchiveConfigTemplateId { get; set; }

    public ArchiveConfigTemplate ArchiveConfigTemplate { get; set; } = null!;

    public int HosterRegistrationId { get; set; }

    public HosterRegistration HosterRegistration { get; set; } = null!;

    public string? Name { get; set; }

    public bool PremiumOnlyDownload { get; set; }

    public List<string> LinksDistributedTo { get; set; } = [];

    public List<UploadConfigLinkCrypterTemplate> LinkCrypterTemplates { get; set; } = [];
}

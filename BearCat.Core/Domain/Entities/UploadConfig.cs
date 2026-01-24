namespace BearCat.Core.Domain.Entities;

public class UploadConfig
{
    public int Id { get; set; }

    public int ReleaseId { get; set; }

    public Release Release { get; set; } = null!;

    public int HosterRegistrationId { get; set; }

    public int ArchiveConfigId { get; set; }

    public ArchiveConfig ArchiveConfig { get; set; } = null!;

    public HosterRegistration HosterRegistration { get; set; } = null!;

    public string Name { get; set; } = null!;

    public List<Upload> Uploads { get; set; } = null!;

    public List<string> LinksDistributedTo { get; set; } = null!;
}

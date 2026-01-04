using BearCat.Core.Domain.ValueObjects;

namespace BearCat.Core.Domain.Entities;

public class ArchiveUpload
{
    public int Id { get; set; }

    public ArchiveUploadState State { get; set; }

    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }

    public List<HosterFile> HosterFiles { get; set; } = null!;

    public List<DistributionArchive> DistributionArchives { get; set; } = null!;
}

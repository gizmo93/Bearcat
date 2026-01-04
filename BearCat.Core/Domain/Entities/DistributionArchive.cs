namespace BearCat.Core.Domain.Entities;

public class DistributionArchive
{
    public int Id { get; set; }
    
    public int DistributionId { get; set; }
    
    public Distribution Distribution { get; set; } = null!;
    
    public List<string> ArchiveFilePaths { get; set; } = new();
    
    public int? ArchiveUploadId { get; set; }
    
    public ArchiveUpload? ArchiveUpload { get; set; }
}

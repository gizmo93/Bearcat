using BearCat.Core.Domain.ValueObjects;

namespace BearCat.Core.Domain.Entities;

public class HosterFile
{
    public int Id { get; set; }
    
    public int DistributionUploadId { get; set; }
    
    public DistributionUpload DistributionUpload { get; set; } = null!;
    
    public string SourceFileName { get; set; } = null!;
    
    public string? FileUrl { get; set; }

    public HosterFileState State { get; set; }
}

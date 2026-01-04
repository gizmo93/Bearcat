using BearCat.Core.Domain.ValueObjects;

namespace BearCat.Core.Domain.Entities;

public class DistributionUpload
{
    public int Id { get; set; }
    
    public int DistributionId { get; set; }
    
    public Distribution Distribution { get; set; } = null!;

    public DistributionUploadState State { get; set; }

    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }
    
    public List<HosterFile> HosterFiles { get; set; } = new();
}

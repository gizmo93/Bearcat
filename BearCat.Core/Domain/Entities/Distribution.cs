namespace BearCat.Core.Domain.Entities;

public class Distribution
{
    public int Id { get; set; }
    
    public int ReleaseId { get; set; }
    
    public Release Release { get; set; } = null!;
    
    public int HosterRegistrationId { get; set; }
    
    public HosterRegistration HosterRegistration { get; set; } = null!;
    
    public string Name { get; set; } = null!;
    
    public string ArchiverFullClassName { get; set; } = null!;
    
    public string? ArchivePassword { get; set; }
    
    public List<DistributionUpload> Uploads { get; set; } = new();
}

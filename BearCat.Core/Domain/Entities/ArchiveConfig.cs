namespace BearCat.Core.Domain.Entities;

public class ArchiveConfig
{
    public int Id { get; set; }
    
    public int ReleaseId { get; set; }
    
    public Release Release { get; set; } = null!;
    
    public string ArchiveFilesBasePath { get; set; } = null!;
    
    public string ArchiverFullClassName { get; set; } = null!;
    
    public string ArchiveNamePrefix { get; set; } = null!;
    
    public string? ArchivePassword { get; set; }
    
    public int ArchiveFileSizeMb { get; set; }
    
    public List<Archive> Archives { get; set; } = new();
    
    public List<UploadConfig> UploadConfigs { get; set; } = new();
}

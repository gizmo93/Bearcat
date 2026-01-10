namespace BearCat.Core.Domain.Entities;

public class Archive
{
    public int Id { get; set; }
    
    public int ArchiveConfigId { get; set; }
    
    public ArchiveConfig ArchiveConfig { get; set; } = null!;
    
    public string ArchiveFolderPath { get; set; } = null!;
    
    public List<ArchiveFile> ArchiveFiles { get; set; } = new();
    
    public List<Upload> Uploads { get; set; } = new();
}

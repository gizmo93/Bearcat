namespace BearCat.Core.Domain.Entities;

public class ArchiveFile
{
    public int Id { get; set; }
    
    public int ArchiveId { get; set; }
    
    public Archive Archive { get; set; } = null!;
    
    public string FullFileName { get; set; } = null!;

    public List<UploadedFile> UploadedFiles { get; set; } = null!;
}

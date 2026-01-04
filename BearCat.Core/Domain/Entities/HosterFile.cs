using BearCat.Core.Domain.ValueObjects;

namespace BearCat.Core.Domain.Entities;

public class HosterFile
{
    public int Id { get; set; }
    
    public int ArchiveUploadId { get; set; }
    
    public ArchiveUpload ArchiveUpload { get; set; } = null!;
    
    public string SourceFileName { get; set; } = null!;
    
    public string? FileUrl { get; set; }

    public HosterFileState State { get; set; }
}

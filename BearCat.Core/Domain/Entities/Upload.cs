using BearCat.Core.Domain.ValueObjects;

namespace BearCat.Core.Domain.Entities;

public class Upload
{
    public int Id { get; set; }
    
    public int UploadConfigId { get; set; }
    
    public UploadConfig UploadConfig { get; set; } = null!;
    
    public int? ArchiveId { get; set; }
    
    public Archive? Archive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UploadedAt { get; set; }

    public UploadState UploadState { get; set; }

    public OnlineState OnlineState { get; set; }
    
    public List<UploadedFile> UploadedFiles { get; set; } = new();
}

namespace Bearcat.Domain.Entities;

public class ArchiveFile
{
    public int Id { get; set; }

    public int ArchiveId { get; set; }

    public Archive Archive { get; set; } = null!;

    public string FullFileName { get; set; } = null!;

    public string? Md5Hash { get; set; }

    public List<UploadedFile> UploadedFiles { get; set; } = null!;
}

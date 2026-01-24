using BearCat.Core.Domain.ValueObjects;

namespace BearCat.Core.Domain.Entities;

public class UploadedFile
{
    public int Id { get; set; }

    public int UploadId { get; set; }

    public Upload Upload { get; set; } = null!;

    public int ArchiveFileId { get; set; }

    public ArchiveFile ArchiveFile { get; set; } = null!;

    public string HosterFileLink { get; set; } = null!;

    public OnlineState OnlineState { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CheckedAt { get; set; }
}

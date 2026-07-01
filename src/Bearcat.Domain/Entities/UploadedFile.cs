using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class UploadedFile
{
    public int Id { get; set; }

    public int UploadId { get; set; }

    public Upload Upload { get; set; } = null!;

    public int ArchiveFileId { get; set; }

    public ArchiveFile ArchiveFile { get; set; } = null!;

    public string HosterFileLink { get; set; } = null!;

    public string? ExternalId { get; set; }

    public List<string> ErrorMessages { get; set; } = [];

    public OnlineState OnlineState { get; set; }

    public int? DownloadCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CheckedAt { get; set; }
}

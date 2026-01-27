using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class Archive
{
    public int Id { get; set; }

    public int ArchiveConfigId { get; set; }

    public ArchiveConfig ArchiveConfig { get; set; } = null!;

    public string ArchiveFolderPath { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public ArchiveState ArchiveState { get; set; }

    public List<string> ErrorMessages { get; set; } = new();

    public List<ArchiveFile> ArchiveFiles { get; set; } = null!;

    public List<Upload> Uploads { get; set; } = null!;

    public List<Notification> Notifications { get; set; } = null!;
}

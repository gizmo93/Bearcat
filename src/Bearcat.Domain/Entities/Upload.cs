using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class Upload
{
    public int Id { get; set; }

    public int UploadConfigId { get; set; }

    public UploadConfig UploadConfig { get; set; } = null!;

    public int? ArchiveId { get; set; }

    public Archive? Archive { get; set; }

    public string? HosterFolderId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UploadedAt { get; set; }

    public UploadState UploadState { get; set; }

    public OnlineState OnlineState { get; set; }

    public DateTime? NotFullyOnlineSince { get; set; }

    public bool PremiumOnlyDownload { get; set; }

    public List<UploadedFile> UploadedFiles { get; set; } = null!;

    public List<string> ErrorMessages { get; set; } = [];

    public List<LinkCrypterContainer> LinkCrypterContainers { get; set; } = null!;

    public List<Notification> Notifications { get; set; } = null!;
}

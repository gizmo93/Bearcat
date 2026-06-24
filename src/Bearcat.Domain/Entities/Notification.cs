using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class Notification
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public NotificationType NotificationType { get; set; }

    public string Message { get; set; } = null!;

    public int? UploadId { get; set; }

    public Upload? Upload { get; set; }

    public int? ArchiveId { get; set; }

    public Archive? Archive { get; set; }

    public int? ReleaseId { get; set; }

    public Release? Release { get; set; }

    public int? LinkCrypterContainerId { get; set; }

    public LinkCrypterContainer? LinkCrypterContainer { get; set; }
}

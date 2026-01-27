using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class LinkCrypterContainer
{
    public int Id { get; set; }

    public int UploadConfigLinkCrypterId { get; set; }

    public UploadConfigLinkCrypter UploadConfigLinkCrypter { get; set; } = null!;

    public int UploadId { get; set; }

    public Upload Upload { get; set; } = null!;

    public string? ExternalReference { get; set; }

    public string ContainerUrl { get; set; } = null!;

    public string? Password { get; set; }

    public LinkCrypterContainerState State { get; set; }

    public List<string> Errors { get; set; } = new();

    public DateTime CreatedAt { get; set; }

    public List<Notification> Notifications { get; set; } = null!;
}

using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class LinkCrypterContainer
{
    public int Id { get; set; }

    public LinkCrypterContainerScope Scope { get; set; }

    public int? UploadConfigLinkCrypterId { get; set; }

    public UploadConfigLinkCrypter? UploadConfigLinkCrypter { get; set; }

    public int? UploadId { get; set; }

    public Upload? Upload { get; set; }

    public int? CollectionUploadSlotId { get; set; }

    public CollectionUploadSlot? CollectionUploadSlot { get; set; }

    public int LinkCrypterRegistrationId { get; set; }

    public LinkCrypterRegistration LinkCrypterRegistration { get; set; } = null!;

    public string? ExternalReference { get; set; }

    public string? StatusImageId { get; set; }

    public string ContainerUrl { get; set; } = null!;

    public string? Password { get; set; }

    public bool EnableCaptcha { get; set; } = true;

    public bool EnableContainerDownload { get; set; } = true;

    public bool EnableClickAndLoad { get; set; } = true;

    public LinkCrypterContainerState State { get; set; }

    public List<string> Errors { get; set; } = new();

    public DateTime CreatedAt { get; set; }

    public List<LinkCrypterContainerSourceUpload> SourceUploads { get; set; } = [];

    public List<Notification> Notifications { get; set; } = null!;
}

using Bearcat.Abstractions.LinkCrypter.Results;
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

    public DateTime? UpdateFailedAt { get; set; }

    public List<LinkCrypterContainerSourceUpload> SourceUploads { get; set; } = [];

    public List<Notification> Notifications { get; set; } = null!;

    public void CopySettingsFrom(UploadConfigLinkCrypter linkCrypterConfig)
    {
        Password = linkCrypterConfig.Password;
        EnableCaptcha = linkCrypterConfig.EnableCaptcha;
        EnableContainerDownload = linkCrypterConfig.EnableContainerDownload;
        EnableClickAndLoad = linkCrypterConfig.EnableClickAndLoad;
    }

    public void ApplyCreateContainerResult(CreateContainerResult result, DateTime localNow)
    {
        ContainerUrl = result.IsSuccess ? result.ContainerLink! : string.Empty;
        ExternalReference = result.IsSuccess ? result.ExternalReference : null;
        StatusImageId = result.IsSuccess ? result.StatusImageId : null;
        Errors = result.ErrorMessages.ToList();
        CreatedAt = localNow;
        UpdateFailedAt = null;
        State = result.IsSuccess
            ? LinkCrypterContainerState.Created
            : LinkCrypterContainerState.CreationFailed;
    }

    public void MarkAsUpdated(string? statusImageId)
    {
        StatusImageId = statusImageId ?? StatusImageId;
        Errors = [];
        UpdateFailedAt = null;
        State = LinkCrypterContainerState.Created;
    }

    public void MarkAsUpdateFailed(string? errorMessage, DateTime localNow)
    {
        Errors = string.IsNullOrWhiteSpace(errorMessage) ? [] : [errorMessage];
        UpdateFailedAt = localNow;
        State = LinkCrypterContainerState.UpdateFailed;
    }

    public bool HasContainerUrl() => !string.IsNullOrWhiteSpace(ContainerUrl);
}

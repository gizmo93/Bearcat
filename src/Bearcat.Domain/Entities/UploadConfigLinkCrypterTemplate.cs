using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Entities;

public class UploadConfigLinkCrypterTemplate
{
    public int Id { get; set; }

    public int UploadConfigTemplateId { get; set; }

    public UploadConfigTemplate UploadConfigTemplate { get; set; } = null!;

    public int LinkCrypterRegistrationId { get; set; }

    public LinkCrypterRegistration LinkCrypterRegistration { get; set; } = null!;

    public LinkCrypterContainerScope ContainerScope { get; set; } =
        LinkCrypterContainerScope.Release;

    public string? Password { get; set; }

    public bool EnableCaptcha { get; set; } = true;

    public bool EnableContainerDownload { get; set; } = true;

    public bool EnableClickAndLoad { get; set; } = true;
}

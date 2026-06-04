namespace Bearcat.Domain.Entities;

public class UploadConfigLinkCrypter
{
    public int Id { get; set; }

    public int UploadConfigId { get; set; }

    public UploadConfig UploadConfig { get; set; } = null!;

    public int LinkCrypterRegistrationId { get; set; }

    public LinkCrypterRegistration LinkCrypterRegistration { get; set; } = null!;

    public string? Password { get; set; }

    public bool EnableCaptcha { get; set; } = true;

    public bool EnableContainerDownload { get; set; } = true;

    public bool EnableClickAndLoad { get; set; } = true;

    public List<LinkCrypterContainer> LinkCrypterContainers { get; set; } = null!;
}

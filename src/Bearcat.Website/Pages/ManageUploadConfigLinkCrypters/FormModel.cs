using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.ManageUploadConfigLinkCrypters;

public class FormModel
{
    public int? LinkCrypterRegistrationId { get; set; }

    public string? Password { get; set; }

    public bool EnableCaptcha { get; set; } = true;

    public bool EnableContainerDownload { get; set; } = true;

    public bool EnableClickAndLoad { get; set; } = true;

    public LinkCrypterContainerScope ContainerScope { get; set; } =
        LinkCrypterContainerScope.Release;
}

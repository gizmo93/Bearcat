using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.ManageReleaseTemplates;

public class UploadConfigLinkCrypterTemplateFormModel
{
    public int? LinkCrypterRegistrationId { get; set; }

    public string? Password { get; set; }

    public LinkCrypterContainerScope ContainerScope { get; set; } =
        LinkCrypterContainerScope.Release;

    public bool EnableCaptcha { get; set; } = true;

    public bool EnableContainerDownload { get; set; } = true;

    public bool EnableClickAndLoad { get; set; } = true;

    public bool IsEdit { get; set; }
}

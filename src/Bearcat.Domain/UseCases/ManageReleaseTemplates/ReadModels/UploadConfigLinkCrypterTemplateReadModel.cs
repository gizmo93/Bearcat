using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleaseTemplates.ReadModels;

public record UploadConfigLinkCrypterTemplateReadModel(
    int UploadConfigLinkCrypterTemplateId,
    int LinkCrypterRegistrationId,
    string LinkCrypterRegistrationName,
    string LinkCrypterName,
    LinkCrypterContainerScope ContainerScope,
    string? Password,
    bool EnableCaptcha,
    bool EnableContainerDownload,
    bool EnableClickAndLoad,
    bool SupportsCaptcha,
    bool SupportsContainerDownload,
    bool SupportsClickAndLoad
);

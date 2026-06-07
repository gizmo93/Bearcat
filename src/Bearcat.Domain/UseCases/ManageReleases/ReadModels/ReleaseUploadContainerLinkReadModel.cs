using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases.ReadModels;

public record ReleaseUploadContainerLinkReadModel(
    string LinkCrypterRegistrationName,
    string LinkCrypterClassName,
    string ContainerUrl,
    LinkCrypterContainerScope Scope,
    LinkCrypterContainerState State,
    DateTime CreatedAt,
    bool EnableCaptcha,
    bool EnableContainerDownload,
    bool EnableClickAndLoad,
    bool SupportsCaptcha,
    bool SupportsContainerDownload,
    bool SupportsClickAndLoad,
    IReadOnlyList<string> Errors
);

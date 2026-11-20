using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.ReadModels;

public sealed record LinkCrypterImportReleaseReadModel(
    string ReleaseName,
    int ReleaseTemplateId,
    string ReleaseTemplateName,
    string? PrimaryLanguageCode,
    LinkCrypterImportReleaseAction? ReleaseAction,
    int? ExistingReleaseId,
    string? ReleaseFolderPath,
    string? ArchiveFolderPath,
    bool IsSelected,
    bool HasSelectableContainers,
    IReadOnlyList<LinkCrypterImportContainerReadModel> Containers
);

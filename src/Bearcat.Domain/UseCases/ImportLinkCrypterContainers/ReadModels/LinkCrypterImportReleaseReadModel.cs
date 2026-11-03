using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.ReadModels;

public sealed record LinkCrypterImportReleaseReadModel(
    string ReleaseName,
    LinkCrypterImportReleaseAction? ReleaseAction,
    int? ExistingReleaseId,
    string? ReleaseFolderPath,
    string? ArchiveFolderPath,
    bool IsSelected,
    bool HasImportableContainers,
    IReadOnlyList<LinkCrypterImportContainerReadModel> Containers
);

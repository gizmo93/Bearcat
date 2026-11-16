namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Reimporting;

public sealed record LinkCrypterImportReimportPlanReadModel(
    string ContainerUrl,
    string ImportedReleaseName,
    string? ReleaseCollectionName,
    IReadOnlyList<LinkCrypterImportReimportReleaseReadModel> Releases,
    IReadOnlyList<LinkCrypterImportReimportUploadNotMovedReadModel> UploadsNotMoved,
    IReadOnlyList<string> ConvertedContainerUrls,
    IReadOnlyList<LinkCrypterImportReimportContainerNotConvertedReadModel> ContainersNotConverted,
    IReadOnlyList<LinkCrypterImportReimportBlocker> Blockers
);

using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Importing;

public sealed record CollectionContainerImportResult(
    ReleaseCollection ReleaseCollection,
    CollectionUploadSlot CollectionUploadSlot,
    IReadOnlyList<LinkCrypterImportedRelease> ImportedReleases,
    IReadOnlyList<ArchivePartsMove> ArchivePartsMoves
);

using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Importing;

public sealed record LinkCrypterImportedRelease(
    Release Release,
    bool IsNewRelease,
    IReadOnlyList<LinkCrypterImportMirrorWithUploadConfig> MirrorsWithUploadConfigs,
    IReadOnlyList<Upload> SourceUploads,
    ArchivePartsToMove? ArchivePartsToMove
);

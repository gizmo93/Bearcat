using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;

public sealed record LinkCrypterImportReleasePreview(
    string ReleaseName,
    IReadOnlyList<LinkCrypterImportMirror> Mirrors,
    string? ReleaseFolderPath,
    string? ArchiveFolderPath,
    LinkCrypterImportReleaseAction ReleaseAction,
    int? ExistingReleaseId,
    LinkCrypterImportNotImportableReason? NotImportableReason
);

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;

public sealed record LinkCrypterImportMirrorsOfArchive(
    string ArchiveBaseName,
    IReadOnlyList<LinkCrypterImportMirror> Mirrors
);

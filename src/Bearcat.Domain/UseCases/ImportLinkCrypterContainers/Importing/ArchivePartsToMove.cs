namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Importing;

public sealed record ArchivePartsToMove(
    string ReleaseName,
    string TargetFolderPath,
    IReadOnlyList<string> SourceFilePaths
);

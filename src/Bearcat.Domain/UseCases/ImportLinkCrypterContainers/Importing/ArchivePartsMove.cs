namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Importing;

public sealed record ArchivePartsMove(
    string ReleaseName,
    string TargetFolderPath,
    IReadOnlyList<string> SourceFilePaths
);

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;

public sealed record LinkCrypterImportLocalFileSearchResult(
    Dictionary<string, HashSet<string>> ArchivePartFileNamesByFolderPath,
    List<string> ReleaseFolderBasePaths
);

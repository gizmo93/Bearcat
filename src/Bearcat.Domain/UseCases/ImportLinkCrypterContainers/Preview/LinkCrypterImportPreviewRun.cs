using Bearcat.Domain.Entities;

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;

public class LinkCrypterImportPreviewRun
{
    public required Dictionary<int, ReleaseTemplate> ReleaseTemplatesById { get; init; }

    public required List<string> ReleaseFolderBasePaths { get; init; }

    public required Dictionary<
        string,
        HashSet<string>
    > ArchivePartFileNamesByFolderPath { get; init; }

    public required Dictionary<string, Release> ExistingReleasesByName { get; init; }

    public HashSet<string> ReleaseNamesOfEarlierContainers { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> ReleaseNamesByChosenArchiveFolderPath { get; } =
        new(StringComparer.Ordinal);
}

namespace Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;

public static class LinkCrypterImportMirrorSplitter
{
    public static IReadOnlyList<LinkCrypterImportMirrorsOfArchive> SplitByArchiveBaseName(
        IReadOnlyList<LinkCrypterImportMirror> mirrors
    )
    {
        var splittableMirrors = mirrors
            .Where(HasOnlyArchivePartsWithKnownFileNames)
            .OrderBy(mirror => mirror.MirrorNumber)
            .ToList();

        if (!splittableMirrors.Any(mirror => GetArchiveBaseNames(mirror).Count > 1))
        {
            return [];
        }

        return splittableMirrors
            .SelectMany(GetArchiveBaseNames)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(archiveBaseName => new LinkCrypterImportMirrorsOfArchive(
                ArchiveBaseName: archiveBaseName,
                Mirrors: GetMirrorsWithFilesOfArchive(splittableMirrors, archiveBaseName)
            ))
            .ToList();
    }

    private static bool HasOnlyArchivePartsWithKnownFileNames(LinkCrypterImportMirror mirror)
    {
        return mirror.Files.All(file =>
            file.FileName is not null && ArchivePartFileNames.IsArchivePart(file.FileName)
        );
    }

    private static List<string> GetArchiveBaseNames(LinkCrypterImportMirror mirror)
    {
        return mirror
            .Files.Select(file => ArchivePartFileNames.GetArchiveBaseName(file.FileName!)!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<LinkCrypterImportMirror> GetMirrorsWithFilesOfArchive(
        List<LinkCrypterImportMirror> mirrors,
        string archiveBaseName
    )
    {
        return mirrors
            .Select(mirror =>
                mirror with
                {
                    Files = mirror
                        .Files.Where(file =>
                            string.Equals(
                                ArchivePartFileNames.GetArchiveBaseName(file.FileName!),
                                archiveBaseName,
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                        .ToList(),
                    Decision = null,
                    UploadByBearcatReason = null,
                }
            )
            .Where(mirror => mirror.Files.Count > 0)
            .ToList();
    }
}

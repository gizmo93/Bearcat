using Bearcat.Domain.UseCases.ImportLinkCrypterContainers.Preview;
using Microsoft.Extensions.Localization;

namespace Bearcat.Website.Pages.ImportLinkCrypterContainers;

public static class LinkCrypterImportLocalFilesText
{
    public static string Get(
        IStringLocalizer<UiResource> localizer,
        LinkCrypterImportReleaseAction? releaseAction,
        string? releaseFolderPath,
        string? archiveFolderPath
    )
    {
        return GetWithArchiveFolderText(
            localizer: localizer,
            releaseAction: releaseAction,
            releaseFolderPath: releaseFolderPath,
            archiveFolderText: archiveFolderPath is null
                ? null
                : localizer["LinkCrypterImportArchiveFolder", archiveFolderPath].Value
        );
    }

    public static string Get(
        IStringLocalizer<UiResource> localizer,
        LinkCrypterImportReleasePreview releasePreview,
        bool archivePartsWereMoved
    )
    {
        return GetWithArchiveFolderText(
            localizer: localizer,
            releaseAction: releasePreview.ReleaseAction,
            releaseFolderPath: releasePreview.ReleaseFolderPath,
            archiveFolderText: GetArchiveFolderText(
                localizer: localizer,
                releasePreview: releasePreview,
                archivePartsWereMoved: archivePartsWereMoved
            )
        );
    }

    private static string GetWithArchiveFolderText(
        IStringLocalizer<UiResource> localizer,
        LinkCrypterImportReleaseAction? releaseAction,
        string? releaseFolderPath,
        string? archiveFolderText
    )
    {
        if (releaseAction is not LinkCrypterImportReleaseAction.CreateRelease)
        {
            return "-";
        }

        List<string> localFolderTexts = [];

        if (releaseFolderPath is not null)
        {
            localFolderTexts.Add(localizer["LinkCrypterImportReleaseFolder", releaseFolderPath]);
        }

        if (archiveFolderText is not null)
        {
            localFolderTexts.Add(archiveFolderText);
        }

        return localFolderTexts.Count == 0
            ? localizer["LinkCrypterImportNoLocalFiles"]
            : string.Join("; ", localFolderTexts);
    }

    private static string? GetArchiveFolderText(
        IStringLocalizer<UiResource> localizer,
        LinkCrypterImportReleasePreview releasePreview,
        bool archivePartsWereMoved
    )
    {
        if (releasePreview.ArchiveFolderPath is not { } archiveFolderPath)
        {
            return null;
        }

        if (!releasePreview.ArchiveFolderContainsArchivesOfOtherReleases)
        {
            return localizer["LinkCrypterImportArchiveFolder", archiveFolderPath];
        }

        var subfolderPath = Path.Combine(archiveFolderPath, releasePreview.ReleaseName);

        return archivePartsWereMoved
            ? localizer["LinkCrypterImportArchivePartsMovedIntoSubfolder", subfolderPath]
            : localizer[
                "LinkCrypterImportArchivePartsAreMovedIntoSubfolder",
                archiveFolderPath,
                subfolderPath
            ];
    }
}

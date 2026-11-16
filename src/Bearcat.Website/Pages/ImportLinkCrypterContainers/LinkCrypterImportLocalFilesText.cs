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
        if (releaseAction is not LinkCrypterImportReleaseAction.CreateRelease)
        {
            return "-";
        }

        List<string> localFolderTexts = [];

        if (releaseFolderPath is not null)
        {
            localFolderTexts.Add(localizer["LinkCrypterImportReleaseFolder", releaseFolderPath]);
        }

        if (archiveFolderPath is not null)
        {
            localFolderTexts.Add(localizer["LinkCrypterImportArchiveFolder", archiveFolderPath]);
        }

        return localFolderTexts.Count == 0
            ? localizer["LinkCrypterImportNoLocalFiles"]
            : string.Join("; ", localFolderTexts);
    }

    public static string Get(
        IStringLocalizer<UiResource> localizer,
        LinkCrypterImportReleasePreview releasePreview
    )
    {
        return Get(
            localizer: localizer,
            releaseAction: releasePreview.ReleaseAction,
            releaseFolderPath: releasePreview.ReleaseFolderPath,
            archiveFolderPath: releasePreview.ArchiveFolderPath
        );
    }
}

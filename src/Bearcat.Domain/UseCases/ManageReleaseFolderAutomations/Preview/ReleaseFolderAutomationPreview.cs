namespace Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.Preview;

public record ReleaseFolderAutomationPreview(
    bool BasePathExists,
    IReadOnlyList<ReleaseFolderAutomationPreviewFolder> MatchingFolders
)
{
    public int MatchingFolderCount => MatchingFolders.Count;

    public int UsedFolderCount =>
        MatchingFolders.Count(folder => folder.IsUsedByReleaseArchiveOrRemoteDownload);

    public int UnusedFolderCount => MatchingFolderCount - UsedFolderCount;
}

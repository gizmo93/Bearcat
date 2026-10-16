namespace Bearcat.Domain.UseCases.ManageReleaseFolderAutomations.Preview;

public record ReleaseFolderAutomationPreviewFolder(
    string FolderName,
    string FolderPath,
    bool IsUsedByReleaseArchiveOrRemoteDownload
);

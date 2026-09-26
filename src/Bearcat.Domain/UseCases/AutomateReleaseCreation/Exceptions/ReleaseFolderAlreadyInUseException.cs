using Bearcat.Domain.UseCases.AutomateReleaseCreation.FolderUsage;

namespace Bearcat.Domain.UseCases.AutomateReleaseCreation.Exceptions;

public sealed class ReleaseFolderAlreadyInUseException(
    string folderPath,
    ReleaseFolderUsageKind usageKind
) : Exception(CreateMessage(folderPath, usageKind))
{
    public string FolderPath { get; } = folderPath;

    public ReleaseFolderUsageKind UsageKind { get; } = usageKind;

    private static string CreateMessage(string folderPath, ReleaseFolderUsageKind usageKind)
    {
        return usageKind switch
        {
            ReleaseFolderUsageKind.ReleaseFolder =>
                $"The folder '{folderPath}' is already the release folder of a release.",
            ReleaseFolderUsageKind.UnmanagedArchiveFolder =>
                $"The folder '{folderPath}' is already the archive folder of an unmanaged release.",
            ReleaseFolderUsageKind.RemoteDownloadFolder =>
                $"The folder '{folderPath}' is already the target folder of a remote download.",
            _ => throw new ArgumentOutOfRangeException(
                nameof(usageKind),
                $"Unknown release folder usage kind, {usageKind}"
            ),
        };
    }
}

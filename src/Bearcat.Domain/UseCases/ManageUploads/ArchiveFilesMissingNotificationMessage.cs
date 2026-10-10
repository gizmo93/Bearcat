using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageUploads;

public static class ArchiveFilesMissingNotificationMessage
{
    public static string Get(ReleaseType releaseType)
    {
        return releaseType switch
        {
            ReleaseType.Managed =>
                "The archive assigned upload has missing files. Bearcat will restore them from an online mirror or repackage the release.",
            ReleaseType.Unmanaged =>
                "The archive assigned upload has missing files. Bearcat will restore them from an online mirror if available, otherwise refresh the unmanaged archive after providing the archive files.",
            _ => throw new ArgumentOutOfRangeException(
                nameof(releaseType),
                $"Unknown release type, {releaseType}"
            ),
        };
    }
}

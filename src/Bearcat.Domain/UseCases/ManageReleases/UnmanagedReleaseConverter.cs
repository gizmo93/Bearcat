using Bearcat.Domain.Entities;
using Bearcat.Domain.Shared.ArchiveRetention;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageReleases;

public class UnmanagedReleaseConverter(MirrorCoverageEvaluator mirrorCoverageEvaluator)
{
    public bool IsRecoverableWithoutReleaseFolder(Release release)
    {
        if (release.ArchiveConfigs.Count == 0)
        {
            return false;
        }

        var mirrorUploads = mirrorCoverageEvaluator.GetMirrorUploadsPerArchiveConfig(release);

        return release.ArchiveConfigs.All(config =>
            config.Archives.Any(archive => archive.ArchiveState is ArchiveState.Created)
            || mirrorUploads.ContainsKey(config.Id)
        );
    }

    public static bool HasCreatedArchiveInside(Release release, string? folderPath)
    {
        return release
            .ArchiveConfigs.SelectMany(config => config.Archives)
            .Where(archive => archive.ArchiveState is ArchiveState.Created)
            .Any(archive =>
                FolderPathHelper.IsSameOrSubPath(
                    childPath: archive.ArchiveFolderPath,
                    parentPath: folderPath
                )
            );
    }

    public static void ConvertToUnmanaged(Release release)
    {
        release.ReleaseType = ReleaseType.Unmanaged;
        release.ReleaseFolderPath = null;
    }
}

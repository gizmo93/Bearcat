using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.Entities;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.Shared.ArchiveRetention;

public class MirrorCoverageEvaluator(IHosterFactory hosterFactory)
{
    public Dictionary<int, Upload> GetMirrorUploadsPerArchiveConfig(Release release)
    {
        var mirrorUploads = new Dictionary<int, Upload>();

        foreach (var archiveConfig in release.ArchiveConfigs)
        {
            var mirrorUpload = FindMirrorUpload(archiveConfig);

            if (mirrorUpload is not null)
            {
                mirrorUploads[archiveConfig.Id] = mirrorUpload;
            }
        }

        return mirrorUploads;
    }

    public bool HasFullMirrorCoverage(Release release)
    {
        return release.ArchiveConfigs.Count > 0
            && GetMirrorUploadsPerArchiveConfig(release).Count == release.ArchiveConfigs.Count;
    }

    public Upload? FindMirrorUpload(ArchiveConfig archiveConfig)
    {
        return archiveConfig
            .UploadConfigs.Where(IsMirrorCapable)
            .SelectMany(uploadConfig => uploadConfig.Uploads)
            .Where(upload =>
                upload.UploadedFiles.Count > 0
                && upload.UploadedFiles.All(file =>
                    file.OnlineState == OnlineState.Online
                    && !string.IsNullOrWhiteSpace(file.HosterFileLink)
                )
            )
            .MaxBy(upload => upload.Id);
    }

    public bool IsMirrorCapable(UploadConfig uploadConfig)
    {
        var registration = uploadConfig.HosterRegistration;

        return registration.IsActive
            && registration.UseForMirrorDownloads
            && hosterFactory.GetByName(registration.HosterClassName) is IHosterWithDownload;
    }
}

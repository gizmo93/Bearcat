using Bearcat.Abstractions.Hoster;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Dto;
using Bearcat.Domain.UseCases.ManageUploadConfigs.Repositories;
using Bearcat.Domain.ValueObjects;

namespace Bearcat.Domain.UseCases.ManageUploadConfigs.UploadSources;

public class UploadSourceValidator(
    IUploadConfigReadRepository uploadConfigReadRepository,
    IHosterFactory hosterFactory
)
{
    public static bool AllowsVideoFileUploads(
        ReleaseType releaseType,
        ReleaseContentType releaseContentType
    )
    {
        return releaseType is ReleaseType.Managed
            && releaseContentType is ReleaseContentType.Movie or ReleaseContentType.TvShowEpisode;
    }

    public async Task ThrowIfInvalidAsync(
        UploadSource uploadSource,
        ReleaseType releaseType,
        ReleaseContentType releaseContentType,
        UploadTarget uploadTarget,
        CancellationToken cancellationToken
    )
    {
        if (
            uploadSource == UploadSource.VideoFiles
            && !AllowsVideoFileUploads(releaseType, releaseContentType)
        )
        {
            throw new InvalidUploadSourceException(
                "Video file uploads are only allowed for managed movie or TV show episode releases."
            );
        }

        await ThrowIfUploadTargetDoesNotMatchAsync(uploadSource, uploadTarget, cancellationToken);
    }

    public async Task ThrowIfUploadTargetDoesNotMatchAsync(
        UploadSource uploadSource,
        UploadTarget uploadTarget,
        CancellationToken cancellationToken
    )
    {
        var hosterClassName = await uploadConfigReadRepository.GetHosterClassNameAsync(
            uploadTarget,
            cancellationToken
        );

        var isVideoHoster = hosterFactory.GetByName(hosterClassName) is IVideoHoster;

        switch (uploadSource)
        {
            case UploadSource.VideoFiles when !isVideoHoster:
                throw new InvalidUploadSourceException(
                    "Video file uploads require an upload target of a video hoster."
                );
            case UploadSource.ArchiveFiles when isVideoHoster:
                throw new InvalidUploadSourceException(
                    "Archive file uploads cannot use an upload target of a video hoster."
                );
        }
    }
}

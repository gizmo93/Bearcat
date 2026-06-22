using Bearcat.Domain.UseCases.ManageReleases.ReadModels;

namespace Bearcat.Domain.UseCases.ManageReleases;

public static class ReleaseMediaFileSelector
{
    public static ReleaseMediaFileReadModel? SelectMainVideo(
        IReadOnlyList<ReleaseMediaFileReadModel> mediaFiles
    )
    {
        return mediaFiles
            .Where(file => file.VideoStream is not null)
            .OrderByDescending(file => file.SizeBytes)
            .FirstOrDefault();
    }
}

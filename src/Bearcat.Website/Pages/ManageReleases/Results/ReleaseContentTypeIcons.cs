using Bearcat.Domain.ValueObjects;

namespace Bearcat.Website.Pages.ManageReleases.Results;

public static class ReleaseContentTypeIcons
{
    public static string GetIconName(ReleaseContentType releaseContentType) =>
        releaseContentType switch
        {
            ReleaseContentType.Movie => "film",
            ReleaseContentType.TvShowEpisode => "tv",
            ReleaseContentType.Game => "gamepad-2",
            ReleaseContentType.Other => "file",
            _ => throw new ArgumentOutOfRangeException(
                nameof(releaseContentType),
                releaseContentType,
                null
            ),
        };
}

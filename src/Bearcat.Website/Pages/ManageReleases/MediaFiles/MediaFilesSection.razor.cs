using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Humanizer;
using Microsoft.AspNetCore.Components;

namespace Bearcat.Website.Pages.ManageReleases.MediaFiles;

public partial class MediaFilesSection : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<ReleaseMediaFileReadModel> MediaFiles { get; set; } = [];

    private ReleaseMediaFileReadModel? MainVideo =>
        ReleaseMediaFileSelector.SelectMainVideo(MediaFiles);

    private IReadOnlyList<ReleaseMediaFileReadModel> OrderedMediaFiles
    {
        get
        {
            var mainVideo = MainVideo;
            if (mainVideo is null)
            {
                return MediaFiles;
            }

            return MediaFiles
                .OrderByDescending(file => file.MediaFileId == mainVideo.MediaFileId)
                .ToList();
        }
    }

    private static string FormatFileSize(long sizeBytes) =>
        sizeBytes <= 0 ? "-" : sizeBytes.Bytes().Humanize("0.0");

    private static string FormatDuration(TimeSpan? duration) =>
        duration is null ? "-" : duration.Value.ToString(@"hh\:mm\:ss");
}

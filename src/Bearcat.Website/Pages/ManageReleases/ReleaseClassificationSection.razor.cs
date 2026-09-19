using System.Globalization;
using Bearcat.Domain.UseCases.ManageReleases.ReadModels;
using Bearcat.Website.Formatting;
using Microsoft.AspNetCore.Components;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Website.Pages.ManageReleases;

public partial class ReleaseClassificationSection(TimeProvider timeProvider) : ComponentBase
{
    [Parameter]
    public ReleaseClassificationReadModel? Classification { get; set; }

    private static string FormatSeason(int season) =>
        $"S{season.ToString("00", CultureInfo.InvariantCulture)}";

    private static string FormatEpisode(int episode, int? episodeEnd)
    {
        var start = $"E{episode.ToString("00", CultureInfo.InvariantCulture)}";

        if (episodeEnd is null)
        {
            return start;
        }

        return $"{start}-E{episodeEnd.Value.ToString("00", CultureInfo.InvariantCulture)}";
    }

    private string HumanizeClassifiedAt(DateTime classifiedAt) =>
        timeProvider.Humanize(classifiedAt);
}

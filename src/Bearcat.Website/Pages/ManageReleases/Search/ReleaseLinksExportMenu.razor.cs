using Bearcat.Domain.ValueObjects;
using Bearcat.Website.Controllers;
using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Website.Pages.ManageReleases.Search;

public partial class ReleaseLinksExportMenu(TimeProvider timeProvider)
{
    [Parameter]
    public IReadOnlyList<int>? ReleaseIds { get; set; }

    private PopoverSide MenuSide => ReleaseIds is null ? PopoverSide.Bottom : PopoverSide.Top;

    private PopoverAlign MenuAlign => ReleaseIds is null ? PopoverAlign.End : PopoverAlign.Start;

    private string GetDownloadUrl(OnlineState? onlineState)
    {
        var queryParameters = new List<string>();

        if (onlineState is not null)
        {
            queryParameters.Add($"onlineState={onlineState}");
        }

        if (ReleaseIds is not null)
        {
            queryParameters.AddRange(ReleaseIds.Select(id => $"releaseIds={id}"));
        }

        return queryParameters.Count == 0
            ? "/releases/links-export"
            : $"/releases/links-export?{string.Join("&", queryParameters)}";
    }

    private string GetFileName(OnlineState? onlineState)
    {
        return ReleaseLinksExportController.GetFileName(
            onlineState: onlineState,
            selectedReleasesOnly: ReleaseIds is not null,
            date: timeProvider.GetLocalNow()
        );
    }
}

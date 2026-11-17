using System.Globalization;
using Bearcat.Domain.UseCases.ManageReleases.LinkExport;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Bearcat.Domain.ValueObjects;
using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using TimeProvider = Bearcat.Domain.Shared.TimeProvider;

namespace Bearcat.Website.Controllers;

[ApiController]
[Route("releases/links-export")]
public class ReleaseLinksExportController(
    IReleaseReadRepository releaseReadRepository,
    TimeProvider timeProvider
) : ControllerBase
{
    public static string GetFileName(
        OnlineState? onlineState,
        bool selectedReleasesOnly,
        DateTime date
    )
    {
        var state = onlineState?.ToString().Kebaberize() ?? "all";
        var scope = selectedReleasesOnly ? $"selected-releases-{state}" : state;
        var formattedDate = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return $"bearcat-links-{scope}-{formattedDate}.csv";
    }

    [HttpGet]
    public async Task DownloadAsync(
        [FromQuery] OnlineState? onlineState,
        [FromQuery] List<int> releaseIds,
        CancellationToken cancellationToken = default
    )
    {
        var selectedReleaseIds = releaseIds.Count > 0 ? releaseIds : null;

        var contentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileName = GetFileName(
                onlineState: onlineState,
                selectedReleasesOnly: selectedReleaseIds is not null,
                date: timeProvider.GetLocalNow()
            ),
        };

        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers.ContentDisposition = contentDisposition.ToString();

        await ReleaseLinksCsvWriter.WriteAsync(
            Response.Body,
            releaseReadRepository.StreamLinksForExportAsync(
                uploadOnlineState: onlineState,
                releaseIds: selectedReleaseIds,
                cancellationToken: cancellationToken
            ),
            cancellationToken
        );
    }
}

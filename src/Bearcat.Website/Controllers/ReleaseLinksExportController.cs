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
    public static string GetFileName(OnlineState? onlineState, DateTime date)
    {
        var state = onlineState?.ToString().Kebaberize() ?? "all";
        var formattedDate = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return $"bearcat-links-{state}-{formattedDate}.csv";
    }

    [HttpGet]
    public async Task DownloadAsync(
        [FromQuery] OnlineState? onlineState,
        CancellationToken cancellationToken = default
    )
    {
        var contentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileName = GetFileName(onlineState, timeProvider.GetLocalNow()),
        };

        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers.ContentDisposition = contentDisposition.ToString();

        await ReleaseLinksCsvWriter.WriteAsync(
            Response.Body,
            releaseReadRepository.StreamLinksForExportAsync(onlineState, cancellationToken),
            cancellationToken
        );
    }
}

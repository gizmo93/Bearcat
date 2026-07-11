using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Bearcat.Website.Controllers;

[ApiController]
[Route("releases/{releaseId:int}/cover")]
public class ReleaseCoverController(
    IReleaseReadRepository releaseReadRepository,
    IHttpClientFactory httpClientFactory
) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> DownloadAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var releaseMetadata = await releaseReadRepository.GetReleaseMetadataAsync(
            releaseId,
            cancellationToken
        );
        var coverUrl = releaseMetadata?.CoverUrl;

        if (string.IsNullOrWhiteSpace(coverUrl))
        {
            return NotFound();
        }

        if (
            !Uri.TryCreate(coverUrl, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
        )
        {
            return BadRequest();
        }

        var client = httpClientFactory.CreateClient("cover-download");
        using var response = await client.GetAsync(uri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode);
        }

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
        var fileName = Path.GetFileName(uri.LocalPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = $"release-{releaseId}-cover{GetCoverFileExtension(contentType)}";
        }

        return File(content, contentType, fileName);
    }

    private static string GetCoverFileExtension(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".jpg",
        };
    }
}

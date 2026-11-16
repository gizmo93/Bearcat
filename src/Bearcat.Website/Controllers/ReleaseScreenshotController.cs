using Bearcat.Abstractions;
using Bearcat.Domain.UseCases.ManageReleaseScreenshots.Repositories;
using Bearcat.Domain.UseCases.ManageReleaseScreenshots.Storage;
using Microsoft.AspNetCore.Mvc;

namespace Bearcat.Website.Controllers;

[ApiController]
[Route("releases/screenshots/{releaseScreenshotId:int}")]
public class ReleaseScreenshotController(
    IReleaseScreenshotReadRepository releaseScreenshotReadRepository,
    ReleaseScreenshotDirectory releaseScreenshotDirectory,
    IFileSystemService fileSystemService
) : ControllerBase
{
    public static string GetUrl(int releaseScreenshotId)
    {
        return $"/releases/screenshots/{releaseScreenshotId}";
    }

    [HttpGet]
    public async Task<IActionResult> GetAsync(
        int releaseScreenshotId,
        CancellationToken cancellationToken = default
    )
    {
        var relativeFilePath = await releaseScreenshotReadRepository.GetRelativeFilePathAsync(
            releaseScreenshotId,
            cancellationToken
        );

        if (relativeFilePath is null)
        {
            return NotFound();
        }

        var filePath = releaseScreenshotDirectory.GetFilePath(relativeFilePath);

        if (!fileSystemService.FileExists(filePath))
        {
            return NotFound();
        }

        return PhysicalFile(filePath, "image/jpeg");
    }
}

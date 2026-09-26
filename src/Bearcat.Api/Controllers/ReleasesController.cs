using Bearcat.Api.Contracts;
using Bearcat.Api.Contracts.Archives;
using Bearcat.Api.Contracts.Releases;
using Bearcat.Api.Security;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.Creation;
using Bearcat.Domain.UseCases.AutomateReleaseCreation.Exceptions;
using Bearcat.Domain.UseCases.ManageReleases;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bearcat.Api.Controllers;

[ApiController]
[Route("api/v1/releases")]
public class ReleasesController(
    IReleaseReadRepository releaseReadRepository,
    ReleaseService releaseService,
    ReleaseFromFolderPathCreationService releaseFromFolderPathCreationService
) : ControllerBase
{
    private const string GetReleaseRouteName = "GetRelease";
    private const int MaximumNameLength = 500;

    /// <summary>
    /// Search releases.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ReleaseResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ReleaseResponse>>> SearchAsync(
        [FromQuery] ReleaseSearchRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var query = new ReleaseSearchQuery(
            SearchTerm: request.SearchTerm,
            ReleaseType: request.ReleaseType,
            ReleaseContentType: request.ReleaseContentType,
            PrimaryLanguageCode: request.PrimaryLanguageCode,
            OnlineState: request.OnlineState,
            HosterRegistrationId: request.HosterRegistrationId,
            ArchiverName: request.ArchiverName,
            LinkCrypterRegistrationId: request.LinkCrypterRegistrationId,
            ReleaseGroupId: request.ReleaseGroupId,
            PostedLocationUrl: request.PostedLocationUrl,
            DownloadLink: request.DownloadLink,
            ArchiveFileName: request.ArchiveFileName,
            UploadId: request.UploadId,
            InPostQueue: request.InPostQueue,
            PageIndex: request.PageIndex,
            PageSize: request.PageSize
        );

        var result = await releaseReadRepository.SearchReleasesAsync(query, cancellationToken);

        var items = result.Items.Select(ReleaseResponse.FromReadModel).ToList();

        return Ok(
            new PagedResponse<ReleaseResponse>(
                Items: items,
                TotalCount: result.TotalCount,
                PageIndex: result.PageIndex,
                PageSize: result.PageSize,
                TotalPages: result.TotalPages
            )
        );
    }

    /// <summary>
    /// Get a release.
    /// </summary>
    [HttpGet("{releaseId:int}", Name = GetReleaseRouteName)]
    [ProducesResponseType(typeof(ReleaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReleaseResponse>> GetAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var release = await releaseReadRepository.GetReleaseAsync(releaseId, cancellationToken);

        if (release is null)
        {
            return NotFound();
        }

        return Ok(ReleaseResponse.FromReadModel(release));
    }

    /// <summary>
    /// Create a release from a finished folder using a release template.
    /// </summary>
    /// <remarks>
    /// Works like the folder automation, but without waiting for the folder to become stable, so only use it, if you are sure the folder was completely copied.
    /// The folder path must be the path as seen by the Bearcat process, which is the container path when Bearcat runs in Docker.
    /// Release info resolution and media metadata extraction run synchronously, so the call can take several seconds.
    /// Fails with 409 if the folder is already the release folder of a release, the archive folder of an unmanaged release or the target folder of a remote download.
    /// </remarks>
    [HttpPost]
    [RequiresApiKey]
    [ProducesResponseType(typeof(ReleaseResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReleaseResponse>> CreateAsync(
        CreateReleaseRequest request,
        CancellationToken cancellationToken = default
    )
    {
        Validate(request);

        if (!ModelState.IsValid)
        {
            return ValidationProblem();
        }

        int releaseId;

        try
        {
            releaseId = await releaseFromFolderPathCreationService.CreateAsync(
                folderPath: request.FolderPath,
                releaseTemplateId: request.ReleaseTemplateId,
                name: request.Name,
                primaryLanguageCode: request.PrimaryLanguageCode,
                cancellationToken: cancellationToken
            );
        }
        catch (ReleaseFolderNotFoundException exception)
        {
            ModelState.AddModelError(nameof(request.FolderPath), exception.Message);
            return ValidationProblem();
        }
        catch (ReleaseTemplateNotFoundException exception)
        {
            ModelState.AddModelError(nameof(request.ReleaseTemplateId), exception.Message);
            return ValidationProblem();
        }
        catch (ReleaseFolderAlreadyInUseException exception)
        {
            return Problem(detail: exception.Message, statusCode: StatusCodes.Status409Conflict);
        }

        var release = await releaseReadRepository.GetReleaseAsync(releaseId, cancellationToken);

        return CreatedAtRoute(
            GetReleaseRouteName,
            new { releaseId },
            ReleaseResponse.FromReadModel(release!)
        );
    }

    /// <summary>
    /// Get release metadata.
    /// </summary>
    [HttpGet("{releaseId:int}/metadata")]
    [ProducesResponseType(typeof(ReleaseMetadataResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReleaseMetadataResponse>> GetMetadataAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var metadata = await releaseReadRepository.GetReleaseMetadataAsync(
            releaseId: releaseId,
            cancellationToken: cancellationToken
        );

        if (metadata is null)
        {
            return NotFound();
        }

        return Ok(ReleaseMetadataResponse.FromReadModel(metadata));
    }

    /// <summary>
    /// List archive configurations of a release.
    /// </summary>
    [HttpGet("{releaseId:int}/archives")]
    [ProducesResponseType(typeof(IReadOnlyList<ArchiveConfigResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ArchiveConfigResponse>>> GetArchivesAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        var release = await releaseReadRepository.GetReleaseAsync(releaseId, cancellationToken);

        if (release is null)
        {
            return NotFound();
        }

        var archiveConfigs = await releaseReadRepository.GetArchiveConfigsAsync(
            releaseId: releaseId,
            cancellationToken: cancellationToken
        );

        var items = archiveConfigs.Select(ArchiveConfigResponse.FromReadModel).ToList();

        return Ok(items);
    }

    /// <summary>
    /// Mark the uploads of a release as posted.
    /// </summary>
    /// <remarks>
    /// Sets UploadsPostedAt to now, which removes the release from the post queue until a newer upload completes. Repeating the call is safe.
    /// </remarks>
    [HttpPost("{releaseId:int}/mark-posted")]
    [RequiresApiKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkPostedAsync(
        int releaseId,
        CancellationToken cancellationToken = default
    )
    {
        if (await releaseReadRepository.GetReleaseAsync(releaseId, cancellationToken) is null)
        {
            return NotFound();
        }

        await releaseService.MarkUploadsPostedAsync(releaseId, cancellationToken);

        return NoContent();
    }

    private void Validate(CreateReleaseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FolderPath))
        {
            ModelState.AddModelError(nameof(request.FolderPath), "The folder path is required.");
        }
        else if (!Path.IsPathFullyQualified(request.FolderPath.Trim()))
        {
            ModelState.AddModelError(
                nameof(request.FolderPath),
                "The folder path must be an absolute path."
            );
        }

        if (request.Name is not null && request.Name.Trim().Length > MaximumNameLength)
        {
            ModelState.AddModelError(
                nameof(request.Name),
                $"The name must be at most {MaximumNameLength} characters long."
            );
        }

        if (
            !string.IsNullOrWhiteSpace(request.PrimaryLanguageCode)
            && !IsTwoLetterCode(request.PrimaryLanguageCode.Trim())
        )
        {
            ModelState.AddModelError(
                nameof(request.PrimaryLanguageCode),
                "The primary language code must be a two-letter ISO 639-1 code."
            );
        }
    }

    private static bool IsTwoLetterCode(string value)
    {
        return value.Length == 2 && value.All(char.IsAsciiLetter);
    }
}

using Bearcat.Api.Contracts;
using Bearcat.Api.Contracts.Uploads;
using Bearcat.Api.Security;
using Bearcat.Domain.UseCases.ManageUploads;
using Bearcat.Domain.UseCases.ManageUploads.Dto;
using Bearcat.Domain.UseCases.ManageUploads.Exceptions;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bearcat.Api.Controllers;

[ApiController]
[Route("api/v1/uploads")]
public class UploadsController(
    IUploadReadRepository uploadReadRepository,
    UploadStateService uploadStateService
) : ControllerBase
{
    private const string GetUploadRouteName = "GetUpload";

    /// <summary>
    /// Search uploads across all releases.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(
        typeof(PagedResponse<UploadWithReleaseResponse>),
        StatusCodes.Status200OK
    )]
    public async Task<ActionResult<PagedResponse<UploadWithReleaseResponse>>> SearchAsync(
        [FromQuery] UploadSearchRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var query = new UploadSearchQuery(
            UploadedAfter: request.UploadedAfter,
            UploadState: request.UploadState,
            OnlineState: request.OnlineState,
            HosterRegistrationId: request.HosterRegistrationId,
            ReleaseId: request.ReleaseId,
            PageIndex: request.PageIndex,
            PageSize: request.PageSize
        );

        var result = await uploadReadRepository.SearchUploadsAsync(query, cancellationToken);

        var items = result.Items.Select(UploadWithReleaseResponse.FromReadModel).ToList();

        return Ok(
            new PagedResponse<UploadWithReleaseResponse>(
                Items: items,
                TotalCount: result.TotalCount,
                PageIndex: result.PageIndex,
                PageSize: result.PageSize,
                TotalPages: result.TotalPages
            )
        );
    }

    /// <summary>
    /// Get an upload.
    /// </summary>
    [HttpGet("{uploadId:int}", Name = GetUploadRouteName)]
    [ProducesResponseType(typeof(UploadWithReleaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UploadWithReleaseResponse>> GetAsync(
        int uploadId,
        CancellationToken cancellationToken = default
    )
    {
        var upload = await uploadReadRepository.GetUploadAsync(uploadId, cancellationToken);

        if (upload is null)
        {
            return NotFound();
        }

        return Ok(UploadWithReleaseResponse.FromReadModel(upload));
    }

    /// <summary>
    /// Create a manual reupload for an offline, partially online, canceled or failed upload.
    /// </summary>
    /// <remarks>
    /// Fails with 409 if the upload is still online or another upload of the same upload config is already pending, running or online.
    /// </remarks>
    [HttpPost("{uploadId:int}/reupload")]
    [RequiresApiKey]
    [ProducesResponseType(typeof(UploadWithReleaseResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UploadWithReleaseResponse>> CreateReuploadAsync(
        int uploadId,
        CancellationToken cancellationToken = default
    )
    {
        if (await uploadReadRepository.GetUploadAsync(uploadId, cancellationToken) is null)
        {
            return NotFound();
        }

        int newUploadId;

        try
        {
            newUploadId = await uploadStateService.CreateManualReuploadAsync(
                uploadId,
                cancellationToken
            );
        }
        catch (InvalidUploadStateException exception)
        {
            return ConflictProblem(exception.Message);
        }

        var newUpload = await uploadReadRepository.GetUploadAsync(newUploadId, cancellationToken);

        return CreatedAtRoute(
            GetUploadRouteName,
            new { uploadId = newUploadId },
            UploadWithReleaseResponse.FromReadModel(newUpload!)
        );
    }

    /// <summary>
    /// Request cancellation of a pending or uploading upload.
    /// </summary>
    /// <remarks>
    /// The running upload picks up the cancellation asynchronously. Requesting cancellation again while it is still pending succeeds.
    /// </remarks>
    [HttpPost("{uploadId:int}/cancel")]
    [RequiresApiKey]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelAsync(
        int uploadId,
        CancellationToken cancellationToken = default
    )
    {
        if (await uploadReadRepository.GetUploadAsync(uploadId, cancellationToken) is null)
        {
            return NotFound();
        }

        if (!await uploadStateService.CancelUploadAsync(uploadId, cancellationToken))
        {
            return ConflictProblem("Only pending or uploading uploads can be canceled.");
        }

        return Accepted();
    }

    /// <summary>
    /// Resume a canceled upload.
    /// </summary>
    /// <remarks>
    /// The upload is queued again and picked up by the upload background task.
    /// </remarks>
    [HttpPost("{uploadId:int}/resume")]
    [RequiresApiKey]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResumeAsync(
        int uploadId,
        CancellationToken cancellationToken = default
    )
    {
        if (await uploadReadRepository.GetUploadAsync(uploadId, cancellationToken) is null)
        {
            return NotFound();
        }

        if (!await uploadStateService.ResumeUploadAsync(uploadId, cancellationToken))
        {
            return ConflictProblem("Only canceled uploads can be resumed.");
        }

        return Accepted();
    }

    /// <summary>
    /// Check the online state of an upload on the hoster now.
    /// </summary>
    /// <remarks>
    /// Runs synchronously against the hoster and returns the updated upload.
    /// </remarks>
    [HttpPost("{uploadId:int}/check-state")]
    [RequiresApiKey]
    [ProducesResponseType(typeof(UploadWithReleaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UploadWithReleaseResponse>> CheckStateAsync(
        int uploadId,
        CancellationToken cancellationToken = default
    )
    {
        if (await uploadReadRepository.GetUploadAsync(uploadId, cancellationToken) is null)
        {
            return NotFound();
        }

        if (!await uploadStateService.CheckUploadStateNowAsync(uploadId, cancellationToken))
        {
            return ConflictProblem(
                "The online state can only be checked for uploads with uploaded files."
            );
        }

        var upload = await uploadReadRepository.GetUploadAsync(uploadId, cancellationToken);

        return Ok(UploadWithReleaseResponse.FromReadModel(upload!));
    }

    private ObjectResult ConflictProblem(string detail)
    {
        return Problem(detail: detail, statusCode: StatusCodes.Status409Conflict);
    }
}

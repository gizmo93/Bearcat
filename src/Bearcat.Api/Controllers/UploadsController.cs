using Bearcat.Api.Contracts;
using Bearcat.Api.Contracts.Uploads;
using Bearcat.Domain.UseCases.ManageUploads.Dto;
using Bearcat.Domain.UseCases.ManageUploads.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bearcat.Api.Controllers;

[ApiController]
[Route("api/v1/uploads")]
public class UploadsController(IUploadReadRepository uploadReadRepository) : ControllerBase
{
    /// <summary>
    /// Search uploads across all releases.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(
        typeof(PagedResponse<UploadSearchResultResponse>),
        StatusCodes.Status200OK
    )]
    public async Task<ActionResult<PagedResponse<UploadSearchResultResponse>>> SearchAsync(
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

        var items = result.Items.Select(UploadSearchResultResponse.FromReadModel).ToList();

        return Ok(
            new PagedResponse<UploadSearchResultResponse>(
                Items: items,
                TotalCount: result.TotalCount,
                PageIndex: result.PageIndex,
                PageSize: result.PageSize,
                TotalPages: result.TotalPages
            )
        );
    }
}

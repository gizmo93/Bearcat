using Bearcat.Api.Contracts;
using Bearcat.Api.Contracts.Uploads;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bearcat.Api.Controllers;

[ApiController]
[Route("api/v1/releases/{releaseId:int}/uploads")]
public class ReleaseUploadsController(IReleaseReadRepository releaseReadRepository) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<UploadResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<UploadResponse>>> SearchAsync(
        int releaseId,
        [FromQuery] ReleaseUploadSearchRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var release = await releaseReadRepository.GetReleaseAsync(releaseId, cancellationToken);

        if (release is null)
        {
            return NotFound();
        }

        var query = new ReleaseUploadSearchQuery(
            ReleaseId: releaseId,
            UploadConfigId: request.UploadConfigId,
            PageIndex: request.PageIndex,
            PageSize: request.PageSize
        );

        var result = await releaseReadRepository.SearchUploadsAsync(query, cancellationToken);
        var items = result.Items.Select(UploadResponse.FromReadModel).ToList();

        return Ok(
            new PagedResponse<UploadResponse>(
                Items: items,
                TotalCount: result.TotalCount,
                PageIndex: result.PageIndex,
                PageSize: result.PageSize,
                TotalPages: result.TotalPages
            )
        );
    }

    [HttpGet("{uploadId:int}/links")]
    [ProducesResponseType(typeof(PagedResponse<UploadLinkResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<UploadLinkResponse>>> SearchLinksAsync(
        int releaseId,
        int uploadId,
        [FromQuery] UploadLinkSearchRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (!await releaseReadRepository.UploadExistsAsync(releaseId, uploadId, cancellationToken))
        {
            return NotFound();
        }

        var query = new ReleaseUploadLinkSearchQuery(
            ReleaseId: releaseId,
            UploadId: uploadId,
            OnlineState: request.OnlineState,
            PageIndex: request.PageIndex,
            PageSize: request.PageSize
        );

        var result = await releaseReadRepository.SearchUploadLinksAsync(query, cancellationToken);
        List<UploadLinkResponse> items = [.. result.Items.Select(UploadLinkResponse.FromReadModel)];

        return Ok(
            new PagedResponse<UploadLinkResponse>(
                Items: items,
                TotalCount: result.TotalCount,
                PageIndex: result.PageIndex,
                PageSize: result.PageSize,
                TotalPages: result.TotalPages
            )
        );
    }

    [HttpGet("{uploadId:int}/linkcrypter-links")]
    [ProducesResponseType(typeof(IReadOnlyList<LinkCrypterLinkResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<
        ActionResult<IReadOnlyList<LinkCrypterLinkResponse>>
    > GetLinkCrypterLinksAsync(
        int releaseId,
        int uploadId,
        CancellationToken cancellationToken = default
    )
    {
        if (!await releaseReadRepository.UploadExistsAsync(releaseId, uploadId, cancellationToken))
        {
            return NotFound();
        }

        var containerLinks = await releaseReadRepository.GetUploadContainerLinksAsync(
            releaseId,
            uploadId,
            cancellationToken
        );
        List<LinkCrypterLinkResponse> items =
        [
            .. containerLinks.Select(LinkCrypterLinkResponse.FromReadModel),
        ];

        return Ok(items);
    }
}

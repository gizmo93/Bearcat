using Bearcat.Api.Contracts;
using Bearcat.Domain.UseCases.ManageReleases.Dto;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bearcat.Api.Controllers;

[ApiController]
[Route("api/v1/releases")]
public class ReleasesController(IReleaseReadRepository releaseReadRepository) : ControllerBase
{
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
            PageIndex: request.PageIndex,
            PageSize: request.PageSize
        );

        var result = await releaseReadRepository.SearchReleasesAsync(query, cancellationToken);
        List<ReleaseResponse> items = [.. result.Items.Select(ReleaseResponse.FromReadModel)];

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

    [HttpGet("{releaseId:int}")]
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
}

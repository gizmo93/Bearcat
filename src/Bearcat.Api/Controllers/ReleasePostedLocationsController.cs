using Bearcat.Api.Contracts;
using Bearcat.Api.Contracts.PostedLocations;
using Bearcat.Api.Security;
using Bearcat.Domain.UseCases.ManageDistributionSites.Repositories;
using Bearcat.Domain.UseCases.ManageForumPostTemplates.Repositories;
using Bearcat.Domain.UseCases.ManagePostedLocations;
using Bearcat.Domain.UseCases.ManagePostedLocations.Dto;
using Bearcat.Domain.UseCases.ManagePostedLocations.Repositories;
using Bearcat.Domain.UseCases.ManageReleases.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bearcat.Api.Controllers;

[ApiController]
[Route("api/v1/releases/{releaseId:int}/posted-locations")]
public class ReleasePostedLocationsController(
    IReleaseReadRepository releaseReadRepository,
    IPostedLocationReadRepository postedLocationReadRepository,
    IDistributionSiteRegistrationReadRepository distributionSiteRegistrationReadRepository,
    IForumPostTemplateReadRepository forumPostTemplateReadRepository,
    PostedLocationService postedLocationService
) : ControllerBase
{
    private const string GetReleasePostedLocationRouteName = "GetReleasePostedLocation";

    /// <summary>
    /// Search posted locations of a release.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<PostedLocationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<PostedLocationResponse>>> SearchAsync(
        int releaseId,
        [FromQuery] ReleasePostedLocationSearchRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (await releaseReadRepository.GetReleaseAsync(releaseId, cancellationToken) is null)
        {
            return NotFound();
        }

        var query = new ReleasePostedLocationSearchQuery(
            ReleaseId: releaseId,
            PageIndex: request.PageIndex,
            PageSize: request.PageSize
        );

        var result = await postedLocationReadRepository.SearchForReleaseAsync(
            query,
            cancellationToken
        );

        var items = result.Items.Select(PostedLocationResponse.FromReadModel).ToList();

        return Ok(
            new PagedResponse<PostedLocationResponse>(
                Items: items,
                TotalCount: result.TotalCount,
                PageIndex: result.PageIndex,
                PageSize: result.PageSize,
                TotalPages: result.TotalPages
            )
        );
    }

    /// <summary>
    /// Get a posted location of a release.
    /// </summary>
    [HttpGet("{postedLocationId:int}", Name = GetReleasePostedLocationRouteName)]
    [ProducesResponseType(typeof(PostedLocationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PostedLocationResponse>> GetAsync(
        int releaseId,
        int postedLocationId,
        CancellationToken cancellationToken = default
    )
    {
        var postedLocation = await postedLocationReadRepository.GetByIdForReleaseAsync(
            releaseId,
            postedLocationId,
            cancellationToken
        );

        if (postedLocation is null)
        {
            return NotFound();
        }

        return Ok(PostedLocationResponse.FromReadModel(postedLocation));
    }

    /// <summary>
    /// Save, where a release was posted.
    /// </summary>
    /// <remarks>
    /// If the release already has a posted location with the same URL, nothing is created and the existing posted location is returned with 200. Otherwise the posted location is created and returned with 201.
    /// </remarks>
    [HttpPost]
    [RequiresApiKey]
    [ProducesResponseType(typeof(PostedLocationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PostedLocationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PostedLocationResponse>> AddAsync(
        int releaseId,
        AddPostedLocationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (await releaseReadRepository.GetReleaseAsync(releaseId, cancellationToken) is null)
        {
            return NotFound();
        }

        await ValidateAsync(request, cancellationToken);

        if (!ModelState.IsValid)
        {
            return ValidationProblem();
        }

        var result = await postedLocationService.AddForReleaseIfUrlIsNewAsync(
            releaseId: releaseId,
            url: request.Url,
            distributionSiteRegistrationId: request.DistributionSiteRegistrationId,
            forumPostTemplateId: request.ForumPostTemplateId,
            cancellationToken: cancellationToken
        );

        var postedLocation = await postedLocationReadRepository.GetByIdForReleaseAsync(
            releaseId,
            result.PostedLocationId,
            cancellationToken
        );
        var response = PostedLocationResponse.FromReadModel(postedLocation!);

        if (!result.WasCreated)
        {
            return Ok(response);
        }

        return CreatedAtRoute(
            GetReleasePostedLocationRouteName,
            new { releaseId, postedLocationId = result.PostedLocationId },
            response
        );
    }

    private async Task ValidateAsync(
        AddPostedLocationRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!IsAbsoluteHttpUrl(request.Url))
        {
            ModelState.AddModelError(
                nameof(request.Url),
                "The URL must be an absolute http or https URL."
            );
        }

        if (
            request.DistributionSiteRegistrationId is { } distributionSiteRegistrationId
            && await distributionSiteRegistrationReadRepository.GetByIdAsync(
                distributionSiteRegistrationId,
                cancellationToken
            )
                is null
        )
        {
            ModelState.AddModelError(
                nameof(request.DistributionSiteRegistrationId),
                $"Distribution site registration {distributionSiteRegistrationId} does not exist."
            );
        }

        if (
            request.ForumPostTemplateId is { } forumPostTemplateId
            && await forumPostTemplateReadRepository.GetDetailAsync(
                forumPostTemplateId,
                cancellationToken
            )
                is null
        )
        {
            ModelState.AddModelError(
                nameof(request.ForumPostTemplateId),
                $"Forum post template {forumPostTemplateId} does not exist."
            );
        }
    }

    private static bool IsAbsoluteHttpUrl(string url)
    {
        return Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}

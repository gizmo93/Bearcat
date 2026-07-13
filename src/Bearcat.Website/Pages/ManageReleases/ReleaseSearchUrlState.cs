using Bearcat.Domain.UseCases.ManageReleases.Dto;

namespace Bearcat.Website.Pages.ManageReleases;

public record ReleaseSearchUrlState(ReleaseSearchQuery Query, int PageIndex, int PageSize);

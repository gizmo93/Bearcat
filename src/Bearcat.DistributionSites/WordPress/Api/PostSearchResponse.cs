namespace Bearcat.DistributionSites.WordPress.Api;

public sealed record PostSearchResponse(int Id, string Status, int Author, PostTitleResponse Title);

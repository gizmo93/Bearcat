namespace Bearcat.Abstractions.DistributionSite.Dto;

public sealed record ExistingBlogPost(string Url, bool IsPublished, bool IsOwnedByAccount);
